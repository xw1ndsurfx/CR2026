using Microsoft.Extensions.Logging;
using Steamworks;

namespace Intersect.Client.ThirdParty;

public static partial class Steam
{
    private const string CorpsRoyauxWebApiIdentity = "corps-royaux";
    private static Callback<GetTicketForWebApiResponse_t>? _webApiTicketCallback;
    private static bool _webApiTicketPending;

    public static bool TryRequestCorpsRoyauxLoginTicket(Action<string?> completion)
    {
        if (!Initialized || _webApiTicketPending)
        {
            return false;
        }

        _webApiTicketPending = true;
        _webApiTicketCallback ??= Callback<GetTicketForWebApiResponse_t>.Create(
            response =>
            {
                _webApiTicketPending = false;

                if (response.m_eResult != EResult.k_EResultOK ||
                    response.m_rgubTicket == null ||
                    response.m_cubTicket <= 0 ||
                    response.m_cubTicket > response.m_rgubTicket.Length)
                {
                    ApplicationContext.Context.Value?.Logger.LogWarning(
                        "Steam login ticket request failed with result {Result}.",
                        response.m_eResult
                    );
                    completion(null);
                    return;
                }

                completion(Convert.ToHexString(response.m_rgubTicket.AsSpan(0, response.m_cubTicket)));
            }
        );

        try
        {
            SteamUser.GetAuthTicketForWebApi(CorpsRoyauxWebApiIdentity);
            return true;
        }
        catch (Exception exception)
        {
            _webApiTicketPending = false;
            ApplicationContext.Context.Value?.Logger.LogError(
                exception,
                "Unable to request a Steam Web API authentication ticket."
            );
            return false;
        }
    }
}
