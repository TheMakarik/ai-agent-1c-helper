using Ai1CAgent.Core.Models;
using Ai1CAgent.Core.Options;

namespace Ai1CAgent.Modules.Ai;

/// <summary>
/// Разрешённые параметры клиента: настройки провайдера, формат API, адрес и ключ.
/// </summary>
internal readonly record struct ResolvedClientSettings(
    AiProviderSettings Settings,
    AiApiFormat Format,
    string Endpoint,
    string ApiKey);
