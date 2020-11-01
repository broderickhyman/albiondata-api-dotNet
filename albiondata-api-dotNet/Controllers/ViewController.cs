using AlbionData.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace albiondata_api_dotNet.Controllers
{
  public class ViewController : Controller
  {
    private readonly MainContext context;

    public ViewController(MainContext context)
    {
      this.context = context;
    }

    [HttpGet("api/v1/stats/[controller]/{itemList}")]
    [ApiExplorerSettings(GroupName = "v1")]
    public ViewResult Index([FromRoute] string itemList, [FromQuery(Name = "locations")] string locationList)
    {
      return View(PricesController.GetMarketByItemId(context, itemList, locationList, null, ApiVersion.One));
    }

    [HttpGet("api/v2/stats/[controller]/{itemList}")]
    [ApiExplorerSettings(GroupName = "v2")]
    public ViewResult Index([FromRoute] string itemList, [FromQuery(Name = "locations")] string locationList, [FromQuery(Name = "qualities")] string qualityList)
    {
      return View(PricesController.GetMarketByItemId(context, itemList, locationList, qualityList, ApiVersion.Two));
    }
  }
}
