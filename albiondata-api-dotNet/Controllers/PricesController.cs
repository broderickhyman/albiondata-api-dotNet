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
  public class PricesController : ControllerBase
  {
    private readonly MainContext context;
    private static readonly string[] AuctionTypes = new[] { "request", "offer" };
    private static readonly int[] AggregateTypes = new[] { 0, 1 };

    public PricesController(MainContext context)
    {
      this.context = context;
    }

    [HttpGet("{itemId}")]
    public ActionResult<IEnumerable<MarketResponse>> Get([FromRoute]string itemId)
    {
      return Ok(GetMarketByItemId(context, itemId));
    }

    public static IEnumerable<MarketResponse> GetMarketByItemId(MainContext context, string itemId)
    {
      var items = context.MarketOrders
        .Where(x => EF.Functions.Like(x.ItemTypeId, itemId.Replace('*', '%')) && x.UpdatedAt > DateTime.UtcNow.AddDays(-1 * Program.MaxAge) && !x.DeletedAt.HasValue)
        .ToArray();
      Debug.WriteLine(items.Length);
      var groups = items.GroupBy(x => new { x.ItemTypeId, x.LocationId });
      var responses = new List<MarketResponse>();
      foreach (var group in groups.OrderBy(x => x.Key.LocationId))
      {
        var dict = new Dictionary<string, UpdatedAggregate>();
        foreach (var auctionType in AuctionTypes)
        {
          foreach (var aggregateType in AggregateTypes)
          {
            var key = $"{auctionType}:{aggregateType}";
            dict[key] = new UpdatedAggregate
            {
              UpdatedAt = DateTime.MinValue,
              Value = 0
            };
          }
        }
        foreach (var auctionType in AuctionTypes)
        {
          foreach (var order in group.Where(x => string.Equals(x.AuctionType, auctionType, StringComparison.OrdinalIgnoreCase)))
          {
            foreach (var aggregateType in AggregateTypes)
            {
              var key = $"{auctionType}:{aggregateType}";
              var current = dict[key];
              var u = order.UpdatedAt;
              var updatedGroup = new DateTime(u.Year, u.Month, u.Day, u.Hour, u.Minute, 0);
              if (updatedGroup > current.UpdatedAt)
              {
                current.UpdatedAt = updatedGroup;
                current.Value = order.UnitPriceSilver;
              }
              else if (updatedGroup == current.UpdatedAt)
              {
                if ((aggregateType == 0 && order.UnitPriceSilver < current.Value) || (aggregateType == 1 && order.UnitPriceSilver > current.Value))
                {
                  current.Value = order.UnitPriceSilver;
                }
              }
            }
          }
        }

        responses.Add(new MarketResponse
        {
          ItemTypeId = group.Key.ItemTypeId,
          City = Locations.GetName(group.Key.LocationId),
          SellPriceMin = dict["offer:0"].Value,
          SellPriceMinDate = dict["offer:0"].UpdatedAt,
          SellPriceMax = dict["offer:1"].Value,
          SellPriceMaxDate = dict["offer:1"].UpdatedAt,
          BuyPriceMin = dict["request:0"].Value,
          BuyPriceMinDate = dict["request:0"].UpdatedAt,
          BuyPriceMax = dict["request:1"].Value,
          BuyPriceMaxDate = dict["request:1"].UpdatedAt
        });
      }
      return responses;
    }

    private class UpdatedAggregate
    {
      public DateTime UpdatedAt;
      public ulong Value;
    }
  }
}
