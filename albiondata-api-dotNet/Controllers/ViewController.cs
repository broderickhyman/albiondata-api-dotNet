using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlbionData.Models;
using Microsoft.AspNetCore.Mvc;

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
    public ViewResult Index([FromRoute]string itemList, [FromQuery(Name = "locations")] string locationList)
    {
      Utilities.SetElasticTransactionName("GET View v1");
      return View(PricesController.GetMarketByItemId(context, itemList, locationList, null, ApiVersion.One));
    }

    [HttpGet("api/v2/stats/[controller]/{itemList}")]
    [ApiExplorerSettings(GroupName = "v2")]
    public ViewResult Index([FromRoute]string itemList, [FromQuery(Name = "locations")] string locationList, [FromQuery(Name = "qualities")] string qualityList)
    {
      Utilities.SetElasticTransactionName("GET View v2");
      return View(PricesController.GetMarketByItemId(context, itemList, locationList, qualityList, ApiVersion.Two));
    }
  }
}
