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
  [FormatFilter]
  public class ChartsController : ControllerBase
  {
    private readonly MainContext context;

    public ChartsController(MainContext context)
    {
      this.context = context;
    }

    [HttpGet("api/v1/stats/[controller]/{itemList}.{format?}")]
    [ApiExplorerSettings(GroupName = "v1")]
    public ActionResult<List<MarketStatChartResponse>> Get([FromRoute] string itemList,
      [FromQuery(Name = "locations")] string locationList,
      [FromQuery] DateTime? date,
      [FromQuery(Name = "end_date")] DateTime? endDate)
    {
      const ApiVersion version = ApiVersion.One;
      return Ok(ConvertToListResponse(GetByItemId(context, itemList, locationList, null, version, date, endDate, 6)));
    }

    [HttpGet("api/v2/stats/[controller]/{itemList}.{format?}")]
    [ApiExplorerSettings(GroupName = "v2")]
    public ActionResult<List<MarketStatChartResponsev2>> Get([FromRoute] string itemList,
      [FromQuery(Name = "locations")] string locationList,
      [FromQuery] DateTime? date,
      [FromQuery(Name = "end_date")] DateTime? endDate,
      [FromQuery(Name = "qualities")] string qualityList,
      [FromQuery(Name = "time-scale")] byte scale = 6)
    {
      const ApiVersion version = ApiVersion.Two;
      return Ok(ConvertToListResponsev2(GetByItemId(context, itemList, locationList, qualityList, version, date, endDate, scale)));
    }

    [HttpGet("api/v2/stats/History/{itemList}.{format?}")]
    [ApiExplorerSettings(GroupName = "v2")]
    public ActionResult<List<MarketHistoriesResponse>> GetHistory([FromRoute] string itemList,
      [FromQuery(Name = "locations")] string locationList,
      [FromQuery] DateTime? date,
      [FromQuery(Name = "end_date")] DateTime? endDate,
      [FromQuery(Name = "qualities")] string qualityList,
      [FromQuery(Name = "time-scale")] byte scale = 6)
    {
      return Ok(ConvertToResponse(GetByItemId(context, itemList, locationList, qualityList, ApiVersion.Two, date, endDate, scale)));
    }

    private List<MarketStatChartResponse> ConvertToListResponse(IEnumerable<MarketHistoryDB> items)
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
          ulong averagePrice = 0;
          if (itemCount > 0)
          {
            averagePrice = silverAmount / itemCount;
          }
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
      }).OrderBy(x => x.Location).ThenBy(x => x.ItemTypeId).ThenBy(x => x.QualityLevel).ToList();
    }

    private List<MarketStatChartResponsev2> ConvertToListResponsev2(IEnumerable<MarketHistoryDB> items)
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
          ulong averagePrice = 0;
          if (itemCount > 0)
          {
            averagePrice = silverAmount / itemCount;
          }
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
      }).OrderBy(x => x.Location).ThenBy(x => x.ItemTypeId).ThenBy(x => x.QualityLevel).ToList();
    }

    private List<MarketHistoriesResponse> ConvertToResponse(IEnumerable<MarketHistoryDB> items)
    {
      return items.OrderBy(x => x.Timestamp).GroupBy(x => new { x.Location, x.ItemTypeId, x.QualityLevel }).Select(mainGroup =>
      {
        var data = new List<MarketHistoryResponse>();
        foreach (var timeGroup in mainGroup.GroupBy(x => x.Timestamp))
        {
          var itemCount = (ulong)timeGroup.Sum(x => (long)x.ItemAmount);
          var silverAmount = (ulong)timeGroup.Sum(x => (long)x.SilverAmount);
          ulong averagePrice = 0;
          if (itemCount > 0)
          {
            averagePrice = silverAmount / itemCount;
          }
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
      }).OrderBy(x => x.Location).ThenBy(x => x.ItemTypeId).ThenBy(x => x.QualityLevel).ToList();
    }

    public static IEnumerable<MarketHistoryDB> GetByItemId(MainContext context, string itemList, string locationList, string qualityList, ApiVersion apiVersion,
      DateTime? date, DateTime? endDate, byte scale, uint count = 0)
    {
      if (string.IsNullOrWhiteSpace(itemList)) itemList = "";
      if (string.IsNullOrWhiteSpace(locationList)) { locationList = ""; }
      if (string.IsNullOrWhiteSpace(qualityList) || apiVersion == ApiVersion.One) qualityList = "";

      if (date == null)
      {
        date = DateTime.UtcNow.AddDays(-30);
      }
      if (endDate == null)
      {
        endDate = date.Value.AddDays(30);
      }

      var itemIds = itemList.Split(",", StringSplitOptions.RemoveEmptyEntries);
      var locations = Utilities.ParseLocationList(locationList);
      var qualities = Utilities.ParseQualityList(qualityList);

      if (itemIds.Length == 0)
      {
        return Enumerable.Empty<MarketHistoryDB>();
      }

      var aggregation = default(TimeAggregation);
      if (scale == 1)
      {
        aggregation = TimeAggregation.Hourly;
      }
      else if (scale == 6 || scale == 24)
      {
        aggregation = TimeAggregation.QuarterDay;
      }

      var itemQuery = context.MarketHistories.AsNoTracking()
        .Where(x => itemIds.Contains(x.ItemTypeId) && x.Timestamp >= date && x.Timestamp <= endDate && x.AggregationType == aggregation);

      if (locations.Any())
      {
        itemQuery = itemQuery.Where(x => locations.Contains(x.Location));
      }
      if (qualities.Any())
      {
        itemQuery = itemQuery.Where(x => qualities.Contains(x.QualityLevel));
      }

      var items = Array.Empty<MarketHistoryDB>();
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
            .Take((int)count)
            .ToArray();
        }
        else
        {
          items = itemQuery.OrderByDescending(x => x.Timestamp)
            .Take((int)count)
            .ToArray();
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

      if (scale == 24)
      {
        // Group results by day
        // Adjust timestamp back 1 minute since the 00:00:00 timestamp should be included as end of day, not beginning
        items = items.GroupBy(x => new { x.Location, x.ItemTypeId, x.QualityLevel, x.Timestamp.AddMinutes(-1).Date })
          .Select(x => new MarketHistoryDB()
          {
            ItemAmount = (ulong)x.Sum(x => (long)x.ItemAmount),
            ItemTypeId = x.Key.ItemTypeId,
            Location = x.Key.Location,
            QualityLevel = x.Key.QualityLevel,
            SilverAmount = (ulong)x.Sum(x => (long)x.SilverAmount),
            Timestamp = x.Key.Date
          })
          .ToArray();
      }

      return items;
    }
  }
}
