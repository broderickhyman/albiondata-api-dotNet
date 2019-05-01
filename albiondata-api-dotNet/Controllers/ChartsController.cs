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
    public ActionResult<IEnumerable<MarketStatChartResponse>> Get([FromRoute] string itemId, [FromQuery(Name = "locations")] string locationList, [FromQuery] uint count, [FromQuery] DateTime? date)
    {
      return Ok(GetByItemId(itemId, locationList, count, date));
    }

    private IEnumerable<MarketStatChartResponse> GetByItemId(string itemId, string locationList, uint count = 720, DateTime? date = null)
    {
      if (string.IsNullOrWhiteSpace(locationList)) { locationList = ""; }
      if (count == 0) { count = 720; }
      if (date == null)
      {
        date = DateTime.UtcNow.AddHours(-1 * count);
      }
      else if (count == 720)
      {
        count = (uint)(DateTime.UtcNow - date).Value.TotalHours;
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

      var items = itemQuery.OrderByDescending(x => x.TimeStamp)
        .Take((int)count)
        .ToArray();
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
  }
}
