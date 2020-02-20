using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AlbionData.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace albiondata_api_dotNet.Controllers
{
  [ApiController]
  [Produces("application/json")]
  public class PricesController : ControllerBase
  {
    private readonly MainContext context;
    private static readonly string[] AuctionTypes = new[] { "request", "offer" };
    private static readonly int[] AggregateTypes = new[] { 0, 1 };

    public PricesController(MainContext context)
    {
      this.context = context;
    }

    [HttpGet("api/v1/stats/[controller]/{itemList}")]
    public ActionResult<IEnumerable<MarketResponse>> Get([FromRoute]string itemList, [FromQuery(Name = "locations")] string locationList)
    {
      Utilities.SetElasticTransactionName("GET Prices v1");
      return Ok(GetMarketByItemId(context, itemList, locationList, null, ApiVersion.One));
    }

    [HttpGet("api/v2/stats/[controller]/{itemList}")]
    [ApiExplorerSettings(GroupName = "v2")]
    public ActionResult<IEnumerable<MarketResponse>> Get([FromRoute]string itemList, [FromQuery(Name = "locations")] string locationList, [FromQuery(Name = "qualities")] string qualityList)
    {
      Utilities.SetElasticTransactionName("GET Prices v2");
      return Ok(GetMarketByItemId(context, itemList, locationList, qualityList, ApiVersion.Two));
    }

    public static IEnumerable<MarketResponse> GetMarketByItemId(MainContext context, string itemList, string locationList, string qualityList, ApiVersion apiVersion)
    {
      if (string.IsNullOrWhiteSpace(itemList)) itemList = "";
      if (string.IsNullOrWhiteSpace(locationList)) locationList = "";
      if (string.IsNullOrWhiteSpace(qualityList) || apiVersion == ApiVersion.One) qualityList = "";

      var itemIds = itemList.Split(",", StringSplitOptions.RemoveEmptyEntries);
      var locations = Utilities.ParseLocationList(locationList);
      var qualities = Utilities.ParseQualityList(qualityList);

      Utilities.SetElasticTransactionLabels(Utilities.ElasticLabel.ItemIds, string.Join(',', itemIds));
      Utilities.SetElasticTransactionLabels(Utilities.ElasticLabel.ItemIdCount, itemIds.Length.ToString());
      Utilities.SetElasticTransactionLabels(Utilities.ElasticLabel.Locations, string.Join(',', locations));
      Utilities.SetElasticTransactionLabels(Utilities.ElasticLabel.Qualities, string.Join(',', qualities));
      if (itemIds.Length == 0)
      {
        return Enumerable.Empty<MarketResponse>();
      }

      // Contains converts to SQL IN clause
      var itemQuery = context.MarketOrders.AsNoTracking()
        .Where(x => itemIds.Contains(x.ItemTypeId) && x.UpdatedAt > DateTime.UtcNow.AddHours(-1 * Program.MaxAge) && !x.DeletedAt.HasValue);
      var historyQuery = context.MarketHistories.FromSqlRaw(@"SELECT
m.*
FROM market_history m
LEFT JOIN market_history m2 ON m.item_id = m2.item_id AND m.id <> m2.id AND m.location = m2.location AND m.quality = m2.quality AND m2.timestamp > m.timestamp
WHERE m2.timestamp IS null
AND m.aggregation = 6")
        .AsNoTracking().Where(x => itemIds.Contains(x.ItemTypeId) && x.Timestamp > DateTime.UtcNow.AddDays(-14));

      if (locations.Any())
      {
        itemQuery = itemQuery.Where(x => locations.Contains(x.LocationId));
        historyQuery = historyQuery.Where(x => locations.Contains(x.Location));
      }
      if (qualities.Any())
      {
        itemQuery = itemQuery.Where(x => qualities.Contains(x.QualityLevel));
        historyQuery = historyQuery.Where(x => qualities.Contains(x.QualityLevel));
      }

      var items = itemQuery.ToArray();
      var historyItems = historyQuery.ToArray();
      Debug.WriteLine(items.Length);
      Debug.WriteLine(historyItems.Length);

      if (apiVersion == ApiVersion.One)
      {
        foreach (var item in items)
        {
          item.QualityLevel = 0;
        }
        foreach (var historyItem in historyItems)
        {
          historyItem.QualityLevel = 0;
        }
      }

      var historyGroups = historyItems.GroupBy(x => new { x.ItemTypeId, x.QualityLevel, x.Location });
      var historyGroupLists = historyGroups.ToDictionary(x => CreateKey(x.Key.ItemTypeId, x.Key.Location, x.Key.QualityLevel), y => y.ToArray());

      var groups = items.GroupBy(x => new { x.ItemTypeId, x.QualityLevel, x.LocationId });
      var itemFoundGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var responses = new List<MarketResponse>();
      foreach (var group in groups)
      {
        var dict = new Dictionary<string, UpdatedAggregate>();
        foreach (var auctionType in AuctionTypes)
        {
          foreach (var aggregateType in AggregateTypes)
          {
            var key = $"{auctionType}:{aggregateType}";
            dict[key] = new UpdatedAggregate
            {
              UpdatedAt = DateTime.MinValue,
              Value = 0
            };
          }
        }
        foreach (var auctionType in AuctionTypes)
        {
          foreach (var order in group.Where(x => string.Equals(x.AuctionType, auctionType, StringComparison.OrdinalIgnoreCase)))
          {
            foreach (var aggregateType in AggregateTypes)
            {
              var key = $"{auctionType}:{aggregateType}";
              var current = dict[key];
              var u = order.UpdatedAt;
              var updatedGroup = new DateTime(u.Year, u.Month, u.Day, u.Hour, u.Minute, 0);
              if (updatedGroup > current.UpdatedAt)
              {
                current.UpdatedAt = updatedGroup;
                current.Value = order.UnitPriceSilver;
              }
              else if (updatedGroup == current.UpdatedAt)
              {
                if ((aggregateType == 0 && order.UnitPriceSilver < current.Value) || (aggregateType == 1 && order.UnitPriceSilver > current.Value))
                {
                  current.Value = order.UnitPriceSilver;
                }
              }
            }
          }
        }

        itemFoundGroups.Add(CreateKey(group.Key.ItemTypeId, group.Key.LocationId, group.Key.QualityLevel));
        responses.Add(new MarketResponse
        {
          ItemTypeId = group.Key.ItemTypeId,
          City = Locations.GetName(group.Key.LocationId),
          QualityLevel = group.Key.QualityLevel,
          SellPriceMin = dict["offer:0"].Value,
          SellPriceMinDate = dict["offer:0"].UpdatedAt,
          SellPriceMax = dict["offer:1"].Value,
          SellPriceMaxDate = dict["offer:1"].UpdatedAt,
          BuyPriceMin = dict["request:0"].Value,
          BuyPriceMinDate = dict["request:0"].UpdatedAt,
          BuyPriceMax = dict["request:1"].Value,
          BuyPriceMaxDate = dict["request:1"].UpdatedAt
        });
      }

      if (!locations.Any())
      {
        locations = new Location[] {
          Location.Caerleon,
          Location.Thetford,
          Location.FortSterling,
          Location.Lymhurst,
          Location.Bridgewatch,
          Location.Martlock,
          Location.BlackMarket
        }.Select(x => (ushort)x);
      }
      if (!qualities.Any())
      {
        if (apiVersion == ApiVersion.One)
        {
          qualities = new byte[] { 0 };
        }
        else
        {
          var foundQualities = groups.Select(x => x.Key.QualityLevel).Union(historyGroups.Select(x => x.Key.QualityLevel)).Distinct();
          if (foundQualities.Any(x => x != 1))
          {
            // If we have found any quality that is not normal assume that they want data pre-filled for all qualities
            // This may need to be rolled back
            qualities = new byte[] { 1, 2, 3, 4, 5 };
          }
          else
          {
            // Only default to normal quality because they may be searching on an item that only has normal quality
            // If they need other qualities filled in they should supply all the qualities in their search parameters
            qualities = new byte[] { 1 };
          }
        }
      }
      foreach (var itemId in itemIds)
      {
        foreach (var locationId in locations)
        {
          foreach (var quality in qualities)
          {
            var key = CreateKey(itemId, locationId, quality);
            if (!itemFoundGroups.Contains(key))
            {
              itemFoundGroups.Add(key);
              // Check if we have historical values for this item
              if (historyGroupLists.TryGetValue(key, out var groupList))
              {
                var itemCount = (ulong)groupList.Sum(x => (long)x.ItemAmount);
                var silverAmount = (ulong)groupList.Sum(x => (long)x.SilverAmount);
                ulong averagePrice = 0;
                if (itemCount > 0)
                {
                  averagePrice = silverAmount / itemCount;
                }
                // Lower resolution to an average of seconds to prevent an overflow when averaging
                var date = new DateTime((long)(groupList.Average(x => x.Timestamp.Ticks / TimeSpan.TicksPerSecond) * TimeSpan.TicksPerSecond));
                responses.Add(new MarketResponse
                {
                  ItemTypeId = itemId,
                  City = Locations.GetName(locationId),
                  QualityLevel = quality,
                  SellPriceMin = averagePrice,
                  SellPriceMinDate = date,
                  SellPriceMax = averagePrice,
                  SellPriceMaxDate = date,
                });
              }
              else
              {
                responses.Add(new MarketResponse
                {
                  ItemTypeId = itemId,
                  City = Locations.GetName(locationId),
                  QualityLevel = quality,
                  SellPriceMin = 0,
                  SellPriceMinDate = DateTime.MinValue,
                  SellPriceMax = 0,
                  SellPriceMaxDate = DateTime.MinValue,
                });
              }
            }
          }
        }
      }

      return responses.OrderBy(x => x.ItemTypeId).ThenBy(x => x.City).ThenBy(x => x.QualityLevel);
    }

    private static string CreateKey(string itemId, ushort locationId, byte quality)
    {
      return $"{itemId}~~{locationId}~~{quality}";
    }

    private class UpdatedAggregate
    {
      public DateTime UpdatedAt;
      public ulong Value;
    }
  }
}
