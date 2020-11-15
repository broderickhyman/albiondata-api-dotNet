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
  public class GoldController : ControllerBase
  {
    private readonly MainContext context;

    public GoldController(MainContext context)
    {
      this.context = context;
    }

    [HttpGet("api/v2/stats/[controller]")]
    [HttpGet("api/v2/stats/[controller].{format}")]
    [ApiExplorerSettings(GroupName = "v2")]
    public ActionResult<IEnumerable<GoldPrice>> Get([FromQuery] DateTime? date, [FromQuery(Name = "count")] int count = 0)
    {
      if (date == null)
      {
        date = DateTime.UtcNow.AddDays(-30);
      }

      var goldQuery = context.GoldPrices.AsNoTracking()
        .Where(x => x.Timestamp > date);
      if (count > 0)
      {
        goldQuery = goldQuery.OrderByDescending(x => x.Timestamp).Take(count);
      }
      else
      {
        goldQuery = goldQuery.OrderBy(x => x.Timestamp);
      }
      var items = goldQuery.ToList();
      foreach (var item in items)
      {
        item.Price /= 10000;
      }
      return Ok(items);
    }
  }
}
