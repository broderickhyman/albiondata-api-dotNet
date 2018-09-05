using AlbionData.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace albiondata_api_dotNet.Controllers
{
  [Produces("application/json")]
  [Route("api/v1/stats/[controller]")]
  [ApiController]
  public class PricesController : ControllerBase
  {
    private readonly MainContext context;

    public PricesController(MainContext context)
    {
      this.context = context;
    }

    [HttpGet("{itemId}")]
    public ActionResult<IEnumerable<MarketResponse>> Get([FromRoute]string itemId)
    {
      //return context.MarketOrders.Where(x => x.ItemTypeId == itemId).Take(10).ToArray();
      var groups = context.MarketOrders
        .Where(x => x.ItemTypeId == itemId)
        .Take(10).ToArray()
        .GroupBy(x => new { x.ItemTypeId, x.LocationId });
      return groups.Select(x => new MarketResponse
      {
        ItemTypeId = x.Key.ItemTypeId,
        City = x.Key.LocationId.ToString(),
        SellPriceMin = x.Min(y => y.UnitPriceSilver),
        SellPriceMax = x.Max(y => y.UnitPriceSilver)
      }).ToArray();
    }
  }
}
