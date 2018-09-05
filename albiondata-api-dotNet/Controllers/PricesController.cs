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
    public ActionResult<IEnumerable<MarketOrderDB>> Get([FromRoute]string itemId)
    {
      return context.MarketOrders.Where(x => x.ItemTypeId == itemId).Take(10).ToArray();
    }
  }
}
