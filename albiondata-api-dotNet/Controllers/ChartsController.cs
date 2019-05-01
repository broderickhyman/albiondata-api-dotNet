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
  [Route("api/v1/stats/[controller]")]
  public class ChartsController : ControllerBase
  {
    private readonly MainContext context;

    public ChartsController(MainContext context)
    {
      this.context = context;
    }

    [HttpGet("{itemId}")]
    public ActionResult<IEnumerable<MarketStatChartResponse>> Get([FromRoute] string itemId, [FromQuery(Name = "locations")] string locationList, [FromQuery] DateTime? date)
    {
      return Ok(ConvertToResponse(GetByItemId(context, itemId, locationList, date)));
    }

    private IEnumerable<MarketStatChartResponse> ConvertToResponse(IEnumerable<MarketStat> items)
    {
      return items.GroupBy(x => x.LocationId).OrderBy(x => x.Key).Select(group => new MarketStatChartResponse
      {
        Location = Locations.GetName(group.Key),
        Data = new MarketStatResponse
        {
          TimeStamps = group.Select(x => (ulong)new DateTimeOffset(x.TimeStamp).ToUnixTimeMilliseconds()).ToList(),
          PricesAvg = group.Select(x => x.PriceAverage).ToList(),
          PricesMax = group.Select(x => x.PriceMax).ToList(),
          PricesMin = group.Select(x => x.PriceMin).ToList()
        }
      });
    }

    public static IEnumerable<MarketStat> GetByItemId(MainContext context, string itemId, string locationList, DateTime? date = null, uint count = 0)
    {
      if (string.IsNullOrWhiteSpace(locationList)) { locationList = ""; }
      if (date == null)
      {
        date = DateTime.UtcNow.AddDays(-30);
      }
      var locationIDs = Utilities.ParseLocationList(locationList);

      var itemQuery = context.MarketStats.AsNoTracking()
        .Where(x => x.ItemId == itemId && x.TimeStamp > date);

      var locationPredicate = PredicateBuilder.False<MarketStat>();
      foreach (var locationID in locationIDs)
      {
        locationPredicate = locationPredicate.Or(x => x.LocationId == locationID);
      }
      if (locationIDs.Any())
      {
        itemQuery = itemQuery.Where(locationPredicate);
      }

      itemQuery = itemQuery.OrderByDescending(x => x.TimeStamp);
      if (count > 0 && locationIDs.Count() == 1)
      {
        itemQuery = itemQuery.Take((int)count);
      }
      return itemQuery.ToArray();
    }
  }
}
