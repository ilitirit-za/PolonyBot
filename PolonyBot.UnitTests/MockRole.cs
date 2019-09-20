using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace PolonyBot.UnitTests
{
    internal class MockRole : IRole
    {
        public MockRole(string name, ulong id)
        {
            Name = name;
            Id = id;
        }

        public ulong Id { get; }
        public DateTimeOffset CreatedAt { get; }
        public Task DeleteAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public string Mention { get; }
        public int CompareTo(IRole other)
        {
            throw new NotImplementedException();
        }

        public Task ModifyAsync(Action<RoleProperties> func, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public IGuild Guild { get; }
        public Color Color { get; }
        public bool IsHoisted { get; }
        public bool IsManaged { get; }
        public bool IsMentionable { get; }
        public string Name { get; }
        public GuildPermissions Permissions { get; }
        public int Position { get; }
    }
}
