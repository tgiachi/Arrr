namespace Arrr.Service.Interfaces;

internal interface IDndService
{
    bool IsEnabled { get; }
    void Set(bool enabled);
}
