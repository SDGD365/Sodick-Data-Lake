using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SodickMastermind.Models;

public sealed class PdfPageJson
{
    public int PageNumber { get; set; }
    public string Text { get; set; } = string.Empty;
}
