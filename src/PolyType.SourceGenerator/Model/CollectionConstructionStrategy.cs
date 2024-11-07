using System;
using System.Collections.Generic;
using System.Text;

namespace PolyType.SourceGenerator.Model;

public enum CollectionConstructionStrategy
{
    None = 0,
    Mutable = 1,
    Span = 2,
    Enumerable = 4,
}