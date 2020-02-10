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
  public class ChartsController : ControllerBase
  {
    private readonly MainContext context;

    public ChartsController(MainContext context)
    {
      this.context = context;
    }

    [HttpGet("api/v1/stats/[controller]/{itemId}")]
    public ActionResult<IEnumerable<MarketStatChartResponse>> Get([FromRoute] string itemId, [FromQuery(Name = "locations")] string locationList, [FromQuery] DateTime? date)
    {
      Utilities.SetElasticTransactionName("GET Charts Stats v1");
      const ApiVersion version = ApiVersion.One;
      return Ok(ConvertToListResponse(GetByItemId(context, itemId, locationList, null, version, date, 6)));
    }

    [HttpGet("api/v2/stats/[controller]/{itemId}")]
    [ApiExplorerSettings(GroupName = "v2")]
    public ActionResult<IEnumerable<MarketStatChartResponsev2>> Get([FromRoute] string itemId, [FromQuery(Name = "locations")] string locationList, [FromQuery] DateTime? date,
      [FromQuery(Name = "qualities")] string qualityList, [FromQuery(Name = "time-scale")] byte scale = 6)
    {
      Utilities.SetElasticTransactionName("GET Charts Stats v2");
      const ApiVersion version = ApiVersion.Two;
      return Ok(ConvertToListResponsev2(GetByItemId(context, itemId, locationList, qualityList, version, date, scale)));
    }

    [HttpGet("api/v2/stats/history/{itemId}")]
    [ApiExplorerSettings(GroupName = "v2")]
    public ActionResult<IEnumerable<MarketHistoriesResponse>> GetHistory([FromRoute] string itemId, [FromQuery(Name = "locations")] string locationList, [FromQuery] DateTime? date,
      [FromQuery(Name = "qualities")] string qualityList, [FromQuery(Name = "time-scale")] byte scale = 6)
    {
      Utilities.SetElasticTransactionName("GET Charts History v2");
      return Ok(ConvertToResponse(GetByItemId(context, itemId, locationList, qualityList, ApiVersion.Two, date, scale)));
    }

    private IEnumerable<MarketStatChartResponse> ConvertToListResponse(IEnumerable<MarketHistoryDB> items)
    {
      return items.OrderBy(x => x.Timestamp).GroupBy(x => new { x.Location, x.ItemTypeId, x.QualityLevel }).Select(mainGroup =>
      {
        var data = new MarketStatResponse()
        {
          ItemCount = new List<ulong>(),
          PricesAvg = new List<decimal>(),
          PricesMax = new List<ulong>(),
          PricesMin = new List<ulong>(),
          Timestamps = new List<ulong>()
        };
        foreach (var timeGroup in mainGroup.GroupBy(x => x.Timestamp))
        {
          var itemCount = (ulong)timeGroup.Sum(x => (long)x.ItemAmount);
          var silverAmount = (ulong)timeGroup.Sum(x => (long)x.SilverAmount);
          var averagePrice = silverAmount / itemCount;
          data.ItemCount.Add(itemCount);
          data.Timestamps.Add((ulong)new DateTimeOffset(timeGroup.Key).ToUnixTimeMilliseconds());
          // Since we are getting these values from the game now, we just have one value
          data.PricesAvg.Add(averagePrice);
          data.PricesMin.Add(averagePrice);
          data.PricesMax.Add(averagePrice);
        }

        return new MarketStatChartResponse
        {
          Location = Locations.GetName(mainGroup.Key.Location),
          ItemTypeId = mainGroup.Key.ItemTypeId,
          QualityLevel = mainGroup.Key.QualityLevel,
          Data = data
        };
      }).OrderBy(x => x.Location).ThenBy(x => x.ItemTypeId).ThenBy(x => x.QualityLevel);
    }

    private IEnumerable<MarketStatChartResponse> ConvertToListResponsev2(IEnumerable<MarketHistoryDB> items)
    {
      return items.OrderBy(x => x.Timestamp).GroupBy(x => new { x.Location, x.ItemTypeId, x.QualityLevel }).Select(mainGroup =>
      {
        var data = new MarketStatResponsev2()
        {
          ItemCount = new List<ulong>(),
          PricesAverage = new List<decimal>(),
          Timestamps = new List<DateTime>()
        };
        foreach (var timeGroup in mainGroup.GroupBy(x => x.Timestamp))
        {
          var itemCount = (ulong)timeGroup.Sum(x => (long)x.ItemAmount);
          var silverAmount = (ulong)timeGroup.Sum(x => (long)x.SilverAmount);
          var averagePrice = silverAmount / itemCount;
          data.ItemCount.Add(itemCount);
          data.Timestamps.Add(timeGroup.Key);
          // Since we are getting these values from the game now, we just have one value
          data.PricesAverage.Add(averagePrice);
        }

        return new MarketStatChartResponsev2
        {
          Location = Locations.GetName(mainGroup.Key.Location),
          ItemTypeId = mainGroup.Key.ItemTypeId,
          QualityLevel = mainGroup.Key.QualityLevel,
          Data = data
        };
      }).OrderBy(x => x.Location).ThenBy(x => x.ItemTypeId).ThenBy(x => x.QualityLevel);
    }

    private IEnumerable<MarketHistoriesResponse> ConvertToResponse(IEnumerable<MarketHistoryDB> items)
    {
      return items.OrderBy(x => x.Timestamp).GroupBy(x => new { x.Location, x.ItemTypeId, x.QualityLevel }).Select(mainGroup =>
      {
        var data = new List<MarketHistoryResponse>();
        foreach (var timeGroup in mainGroup.GroupBy(x => x.Timestamp))
        {
          var itemCount = (ulong)timeGroup.Sum(x => (long)x.ItemAmount);
          var silverAmount = (ulong)timeGroup.Sum(x => (long)x.SilverAmount);
          var averagePrice = silverAmount / itemCount;
          data.Add(new MarketHistoryResponse()
          {
            AveragePrice = averagePrice,
            ItemCount = itemCount,
            Timestamp = timeGroup.Key
          });
        }

        return new MarketHistoriesResponse
        {
          Location = Locations.GetName(mainGroup.Key.Location),
          ItemTypeId = mainGroup.Key.ItemTypeId,
          QualityLevel = mainGroup.Key.QualityLevel,
          Data = data
        };
      }).OrderBy(x => x.Location).ThenBy(x => x.ItemTypeId).ThenBy(x => x.QualityLevel);
    }

    public static IEnumerable<MarketHistoryDB> GetByItemId(MainContext context, string itemId, string locationList, string qualityList, ApiVersion apiVersion, DateTime? date,
      byte scale, uint count = 0)
    {
      if (string.IsNullOrWhiteSpace(locationList)) { locationList = ""; }
      if (string.IsNullOrWhiteSpace(qualityList) || apiVersion == ApiVersion.One) qualityList = "";

      if (date == null)
      {
        date = DateTime.UtcNow.AddDays(-30);
      }
      var locations = Utilities.ParseLocationList(locationList);
      var qualities = Utilities.ParseQualityList(qualityList);

      Utilities.SetElasticTransactionLabels(Utilities.ElasticLabel.DateSearch, date.Value.ToString("s"));
      Utilities.SetElasticTransactionLabels(Utilities.ElasticLabel.ItemIds, itemId);
      Utilities.SetElasticTransactionLabels(Utilities.ElasticLabel.ItemIdCount, "1");
      Utilities.SetElasticTransactionLabels(Utilities.ElasticLabel.Locations, string.Join(',', locations));
      Utilities.SetElasticTransactionLabels(Utilities.ElasticLabel.Qualities, string.Join(',', qualities));

      var aggregation = default(TimeAggregation);
      if (scale == 1)
      {
        aggregation = TimeAggregation.Hourly;
      }
      else if (scale == 6)
      {
        aggregation = TimeAggregation.QuarterDay;
      }

      var itemQuery = context.MarketHistories.AsNoTracking()
        .Where(x => x.ItemTypeId == itemId && x.Timestamp > date && x.AggregationType == aggregation);

      if (locations.Any())
      {
        itemQuery = itemQuery.Where(x => locations.Contains(x.Location));
      }
      if (qualities.Any())
      {
        itemQuery = itemQuery.Where(x => qualities.Contains(x.QualityLevel));
      }

      var items = Enumerable.Empty<MarketHistoryDB>();
      var takeCount = count > 0 && locations.Count() == 1;
      if (takeCount)
      {
        if (apiVersion == ApiVersion.One)
        {
          items = itemQuery.GroupBy(x => new { x.Location, x.ItemTypeId, x.Timestamp })
            .OrderByDescending(x => x.Key.Timestamp)
            .Select(x => new MarketHistoryDB()
            {
              ItemAmount = (ulong)x.Sum(x => (long)x.ItemAmount),
              ItemTypeId = x.Key.ItemTypeId,
              Location = x.Key.Location,
              QualityLevel = 0,
              SilverAmount = (ulong)x.Sum(x => (long)x.SilverAmount),
              Timestamp = x.Key.Timestamp
            })
            .Take((int)count);
        }
        else
        {
          items = itemQuery.OrderByDescending(x => x.Timestamp).Take((int)count);
        }
      }
      else
      {
        items = itemQuery.ToArray();
      }

      if (apiVersion == ApiVersion.One)
      {
        foreach (var item in items)
        {
          item.QualityLevel = 0;
        }
      }

      return items;
    }
  }
}
