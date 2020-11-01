using AlbionData.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace albiondata_api_dotNet
{
  public static class Utilities
  {
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
