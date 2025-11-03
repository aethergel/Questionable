using System;
using System.Text.Json.Serialization;
using Questionable.Model.Common.Converter;

namespace Questionable.Model.Questing
{
  public class LastChecked
  {
    public string? Username { get; set; }
    //[JsonConverter(typeof(DateConverter))]
    public string? Date { get; set; }
  }
}