using dsMIS.Core.DTOs;
using aseeMIS.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.SqlServer.Server;

namespace aseeMIS.Services
{

    public class AppSessionService : IAppSessionService
    {
        // Keys used in session storage
        private const string SessionKey_Language = "LanguageCode";
        private const string SessionKey_UserDto = "DSFormContextDto";

        // Dependency: IHttpContextAccessor to get HttpContext (injected via DI)
        private readonly IHttpContextAccessor _contextAccessor;
        public AppSessionService(IHttpContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
            // Note: We do NOT capture HttpContext here to avoid stale context issues:contentReference[oaicite:9]{index=9}.
            // Instead, we'll use _contextAccessor.HttpContext when accessing session.
        }

        #region Getters (Session Retrieval Methods)
        public string GetLanguageCode()
        {
            // Retrieve the language code string from session (or null if not set)
            return GetFromSession<string>(SessionKey_Language);
        }
        public ContextUserDto GetAuthenticatedUser()
        {
            // Retrieve the complex user DTO from session (deserialized from JSON)
            return GetFromSession<ContextUserDto>(SessionKey_UserDto);
        }
        #endregion

        #region Setters (Session Storage Methods)
        public void SetLanguageCode(string code)
        {
            // Store the language code in session (as a plain string)
            SetInSession(SessionKey_Language, code);
        }
        public void SetAuthenticatedUser(ContextUserDto userDto)
        {
            // Store the user DTO in session (serialized to JSON string)
            SetInSession(SessionKey_UserDto, userDto);
        }

        #endregion

        #region Internal Helper Methods
        /// <summary> 
        /// Generic helper to retrieve an object of type T from session by key. 
        /// Deserializes from JSON if needed.
        /// </summary>
        private T GetFromSession<T>(string key)
        {
            var session = _contextAccessor.HttpContext?.Session;
            if (session == null)
                throw new InvalidOperationException("Session is not available. Ensure session is configured and there is an active HTTP request.");

            string storedValue = session.GetString(key);
            if (string.IsNullOrEmpty(storedValue))
                return default;  // Return default(T) if nothing in session

            // Deserialize JSON string to object of type T (for complex types)
            return System.Text.Json.JsonSerializer.Deserialize<T>(storedValue);
            // (If using Newtonsoft.Json, use JsonConvert.DeserializeObject<T>(storedValue):contentReference[oaicite:10]{index=10})
        }
        /// <summary>
        /// Generic helper to store an object of type T into session as a JSON string.
        /// Removes the key if value is null.
        /// </summary>
        private void SetInSession<T>(string key, T value)
        {
            var session = _contextAccessor.HttpContext?.Session;
            if (session == null)
                throw new InvalidOperationException("Session is not available. Cannot store data.");

            if (value == null)
            {
                session.Remove(key);  // Remove the entry if null is provided (optional clean-up)
            }
            else
            {
                // Serialize the object to JSON and store it in session
                string json = System.Text.Json.JsonSerializer.Serialize(value);
                session.SetString(key, json);
                // (With Newtonsoft.Json: session.SetString(key, JsonConvert.SerializeObject(value)):contentReference[oaicite:11]{index=11})
            }
        }
        #endregion
    }


}
