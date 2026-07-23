using System;
using System.Collections.Generic;
using DCAnt2.Core;
using Xunit;

namespace DCAnt2.Tests;

public class IdGeneratorTests
{
    [Fact]
    public void GeneratedId_HasCorrectPrefixAndFormat()
    {
        var id = IdGenerator.GenerateWithPrefix("ord");
        
        Assert.StartsWith("ord_", id);
        
        var parts = id.Split('_');
        Assert.Equal(3, parts.Length);
        Assert.Equal("ord", parts[0]);
        Assert.Equal(6, parts[1].Length);
        Assert.Equal(12, parts[2].Length);
        Assert.Equal(23, id.Length);
    }

    [Fact]
    public void GeneratedIds_AreUnique()
    {
        var ids = new HashSet<string>();
        for (int i = 0; i < 1000; i++)
        {
            var id = IdGenerator.GenerateWithPrefix("ord");
            Assert.DoesNotContain(id, ids);
            ids.Add(id);
        }
    }
}
