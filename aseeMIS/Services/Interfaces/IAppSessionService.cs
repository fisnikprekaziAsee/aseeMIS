
namespace aseeMIS.Services.Interfaces
{
    public interface IAppSessionService
    {
        // Language code (e.g., "en", "fr") for the current user session
        string GetLanguageCode();
        void SetLanguageCode(string code);

    }
}
