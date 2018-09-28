using AlbionData.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace albiondata_api_dotNet.Controllers
{
  [Route("api/v1/stats/[controller]")]
  public class ViewController : Controller
  {
    private readonly MainContext context;

    public ViewController(MainContext context)
    {
      this.context = context;
    }

    [HttpGet("{itemList}")]
    public ViewResult Index([FromRoute]string itemList, [FromQuery(Name = "locations")] string locationList)
    {
      return View(PricesController.GetMarketByItemId(context, itemList, locationList));
    }
  }
}
