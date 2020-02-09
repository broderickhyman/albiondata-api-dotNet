using AlbionData.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace albiondata_api_dotNet
{
  public static class Utilities
  {
    public static void SetElasticTransactionName(string name)
    {
      if (Elastic.Apm.Agent.Tracer.CurrentTransaction != null)
      {
        Elastic.Apm.Agent.Tracer.CurrentTransaction.Name = name;
      }
    }

    public static void SetElasticTransactionLabels(ElasticLabel elasticLabel, string value)
    {
      if (Elastic.Apm.Agent.Tracer.CurrentTransaction != null)
      {
        var key = "";
        switch (elasticLabel)
        {
          case ElasticLabel.DateSearch:
            key = "date-search";
            break;
          case ElasticLabel.ItemIdCount:
            key = "item-id-count";
            break;
          case ElasticLabel.ItemIds:
            key = "item-ids";
            break;
          case ElasticLabel.Locations:
            key = "locations";
            break;
          case ElasticLabel.Qualities:
            key = "qualities";
            break;
          case ElasticLabel.RequestCount:
            key = "request-count";
            break;
        }
        if (string.IsNullOrEmpty(key) || Elastic.Apm.Agent.Tracer.CurrentTransaction.Labels.ContainsKey(key))
        {
          return;
        }
        Elastic.Apm.Agent.Tracer.CurrentTransaction.Labels[key] = value;
      }
    }

    public enum ElasticLabel
    {
      DateSearch,
      ItemIdCount,
      ItemIds,
      Locations,
      Qualities,
      RequestCount
    }

    public static IEnumerable<ushort> ParseLocationList(string locationString)
    {
      return locationString.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(location =>
      {
        try
        {
          if (!string.Equals(location, "Black Market", StringComparison.OrdinalIgnoreCase))
          {
            location = location.Replace(" Market", "", StringComparison.OrdinalIgnoreCase);
          }
          location = location.Replace(" ", "");
          return (ushort)Enum.Parse<Location>(location, true);
        }
        catch (ArgumentException) { }
        return ushort.MaxValue;
      }).Where(x => x != ushort.MaxValue)
      .OrderBy(x => x);
    }

    public static IEnumerable<byte> ParseQualityList(string qualityList)
    {
      return qualityList.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(quality =>
      {
        if (byte.TryParse(quality, out var result))
        {
          return result;
        }
        return byte.MinValue;
      }).Where(x => x > 0 && x < 6)
      .OrderBy(x => x);
    }
  }
}
