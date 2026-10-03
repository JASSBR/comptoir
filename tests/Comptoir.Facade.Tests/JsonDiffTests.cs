using System.Text.Json.Nodes;
using Comptoir.Facade;

namespace Comptoir.Facade.Tests;

public sealed class JsonDiffTests
{
    [Fact]
    public void Numbers_AreComparedByValue_KeysInAnyOrder() =>
        JsonDiff.Compare(JsonNode.Parse("""{"vatRate":20,"label":"Farine"}"""), JsonNode.Parse("""{"label":"Farine","vatRate":20.00}""")).ShouldBeEmpty();

    [Fact]
    public void Differences_AreReportedWithTheirPath()
    {
        var differences = JsonDiff.Compare(
            JsonNode.Parse("""[{"id":1,"available":238},{"id":2,"available":40}]"""),
            JsonNode.Parse("""[{"id":1,"available":237},{"id":2,"available":40}]"""));

        differences.ShouldBe(["$[0].available: legacy 238, new 237"]);
    }

    [Fact]
    public void MissingFields_ArraySizes_AndTypes_AreDifferences()
    {
        JsonDiff.Compare(JsonNode.Parse("""{"a":1,"b":"x"}"""), JsonNode.Parse("""{"a":1}""")).ShouldBe(["$.b: legacy \"x\", new null"]);
        JsonDiff.Compare(JsonNode.Parse("[1,2]"), JsonNode.Parse("[1]")).ShouldBe(["$: 2 items in the legacy, 1 in the new"]);
        JsonDiff.Compare(JsonNode.Parse("""{"n":"1"}"""), JsonNode.Parse("""{"n":1}""")).Count.ShouldBe(1);
    }

    [Fact]
    public void Output_IsCapped() =>
        JsonDiff.Compare(JsonNode.Parse("[1,2,3,4,5,6,7]"), JsonNode.Parse("[0,0,0,0,0,0,0]"), max: 3).Count.ShouldBe(3);
}
