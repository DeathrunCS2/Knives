namespace Deathrun.Knives.Interfaces;

public interface IBaseInterface
{
    bool Init();

    void OnPostInit()
    {
    }

    void Shutdown()
    {
    }

    void OnAllSharpModulesLoaded()
    {
    }
}
