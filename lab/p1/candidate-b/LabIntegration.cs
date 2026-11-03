using AxisLab;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Variables;

// Macro Deck SDK plugin, with data furnished exclusively by the common TEST adapter.
// A Text variable carries the full TEST envelope: native scalar variables do not preserve HUB metadata.
public sealed class LabIntegration(LabEngine engine) : IPluginIntegration, IVariableProvider
{
    public IReadOnlyList<IActionDefinition> Actions => [];
    public bool IsInitialized { get; private set; }
    public Task InitializeAsync(IIntegrationContext context) { IsInitialized=true; return Task.CompletedTask; }
    public Task ShutdownAsync() { IsInitialized=false; return Task.CompletedTask; }
    public IReadOnlyList<VariableDefinition> Variables => [new() {
        Id="test-snapshot", Name="axis_test_snapshot", Type=VariableType.Text,
        Materialization=VariableMaterialization.Eager, DisplayName="AXIS TEST snapshot" }];
    public ValueTask<VariableReading> ReadAsync(string localId,CancellationToken cancellationToken=default)
        => ValueTask.FromResult(localId=="test-snapshot"
            ? VariableReading.Of(engine.Observe()["snapshot"]!.ToJsonString())
            : VariableReading.Unavailable);
}
