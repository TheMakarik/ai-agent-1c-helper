using Ai1CAgent.Core.Models;

namespace Ai1CAgent.Core.Common;

public interface ISkillStore
{
    IReadOnlyList<string> List(SkillScope scope, long? projectId);

    string? Read(SkillScope scope, long? projectId, string name);

    void Write(SkillScope scope, long? projectId, string name, string content);

    void Delete(SkillScope scope, long? projectId, string name);
}
