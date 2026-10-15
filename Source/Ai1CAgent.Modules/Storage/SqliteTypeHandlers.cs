using System.Data;
using System.Globalization;
using Ai1CAgent.Core.Models;
using Dapper;

namespace Ai1CAgent.Modules.Storage;

internal static class SqliteTypeHandlers
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        _registered = true;
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
        SqlMapper.AddTypeHandler(new ChatRoleHandler());
    }

    private sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        public override DateTimeOffset Parse(object value) =>
            DateTimeOffset.Parse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    private sealed class ChatRoleHandler : SqlMapper.TypeHandler<AiChatRole>
    {
        public override void SetValue(IDbDataParameter parameter, AiChatRole value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString();
        }

        public override AiChatRole Parse(object value) => Enum.Parse<AiChatRole>((string)value);
    }
}
