using AlbionData.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
      return Ok(GetMarketByItemId(context, itemList, locationList, null, ApiVersion.One));
    }

    [HttpGet("api/v2/stats/[controller]/{itemList}")]
    [ApiExplorerSettings(GroupName = "v2")]
    public ActionResult<IEnumerable<MarketResponse>> Get([FromRoute]string itemList, [FromQuery(Name = "locations")] string locationList, [FromQuery(Name = "qualities")] string qualityList)
    {
      return Ok(GetMarketByItemId(context, itemList, locationList, qualityList, ApiVersion.Two));
    }

    public static IEnumerable<MarketResponse> GetMarketByItemId(MainContext context, string itemList, string locationList, string qualityList, ApiVersion apiVersion)
    {
      if (itemList == null) itemList = "";
      if (locationList == null) locationList = "";
      if (qualityList == null || apiVersion == ApiVersion.One) qualityList = "";

      var itemIds = itemList.Split(",", StringSplitOptions.RemoveEmptyEntries);
      var locations = locationList.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(location =>
      {
        if (!string.Equals(location, "Black Market", StringComparison.OrdinalIgnoreCase))
        {
          location = location.Replace(" Market", "", StringComparison.OrdinalIgnoreCase);
        }
        return location.Replace(" ", "");
      });
      var qualities = qualityList.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(quality =>
      {
        if (byte.TryParse(quality, out var result))
        {
          return result;
        }
        return 0;
      }).Where(x => x > 0 && x < 6);

      var queryItems = context.MarketOrders
        .Where(x => x.UpdatedAt > DateTime.UtcNow.AddDays(-1 * Program.MaxAge) && !x.DeletedAt.HasValue);
      var itemTypePredicate = PredicateBuilder.False<MarketOrderDB>();
      var whereCount = 0;
      foreach (var itemId in itemIds)
      {
        itemTypePredicate = itemTypePredicate.Or(x => x.ItemTypeId == itemId);
        whereCount++;
      }
      if (whereCount == 0) return new[] { new MarketResponse() };
      var locationPredicate = PredicateBuilder.False<MarketOrderDB>();
      var qualityPredicate = PredicateBuilder.False<MarketOrderDB>();

      var locationWhereCount = 0;
      foreach (var location in locations)
      {
        try
        {
          var locationId = (ushort)Enum.Parse<Location>(location, true);
          locationPredicate = locationPredicate.Or(x => x.LocationId == locationId);
          locationWhereCount++;
        }
        catch (ArgumentException) { }
      }
      var qualityWhereCount = 0;
      foreach (var quality in qualities)
      {
        qualityPredicate = qualityPredicate.Or(x => x.QualityLevel == quality);
        qualityWhereCount++;
      }

      queryItems = queryItems.Where(itemTypePredicate);
      if (locationWhereCount > 0)
      {
        queryItems = queryItems.Where(locationPredicate);
      }
      if (qualityWhereCount > 0)
      {
        queryItems = queryItems.Where(qualityPredicate);
      }
      var items = queryItems.ToArray();
      Debug.WriteLine(items.Length);

      if (apiVersion == ApiVersion.One)
      {
        foreach (var item in items)
        {
          item.QualityLevel = 0;
        }
      }

      var groups = items.GroupBy(x => new { x.ItemTypeId, x.QualityLevel, x.LocationId });
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

      return responses.OrderBy(x => x.ItemTypeId).ThenBy(x => x.City).ThenBy(x => x.QualityLevel);
    }

    private class UpdatedAggregate
    {
      public DateTime UpdatedAt;
      public ulong Value;
    }
  }
}
