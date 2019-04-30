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
    public ActionResult<IEnumerable<MarketStatChartResponse>> Get([FromRoute]string itemId)
    {
      return Ok(GetByItemId(itemId));
    }

    private IEnumerable<MarketStatChartResponse> GetByItemId(string itemId)
    {
      var items = context.MarketStats.AsNoTracking()
        .Where(x => x.ItemId == itemId)
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
