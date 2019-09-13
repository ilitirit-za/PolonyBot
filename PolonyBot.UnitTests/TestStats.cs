using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using NSubstitute;
using NUnit.Framework;
using PolonyBot.Modules.LFG;
using PolonyBot.Modules.LFG.DAL;

namespace PolonyBot.UnitTests
{
    public class TestStats
    {
        private LfgModule _lfg;
        private ILfgDao _dao;
        private ICommandContext _commandContext;
        private IGuildUser _user;
        private IDMChannel _channel;

        private const ulong DevRoleId = 1;
        private const ulong TesterRoleId = 2;
        private const ulong ModRoleId = 3;
        private const ulong UserRoleId = 4;

        [SetUp]
        public void Setup()
        {
            _dao = Substitute.For<ILfgDao>();
            _user = Substitute.For<IGuildUser>();
            _user.RoleIds.Returns(r => new List<ulong> { ModRoleId } );
            _channel = Substitute.For<IDMChannel>();
            _user.GetOrCreateDMChannelAsync(Arg.Any<RequestOptions>()).Returns(_channel);
            var validRoles = new IRole[]
            {
                new MockRole("PolonyBot-Dev", DevRoleId),
                new MockRole("PolonyBot-Tester", TesterRoleId),
                new MockRole("Moderator", ModRoleId),
                new MockRole("User", UserRoleId),
            };
            
            var guild = Substitute.For<IGuild>();
            guild.Roles.Returns(validRoles);

            _commandContext = Substitute.For<ICommandContext>();
            _commandContext.User.Returns(_user);
            _commandContext.Guild.Returns(guild);

            // Discord.Net isn't designed with simple
            // testability in mind so we have to jump
            // through hoops like these...
            _lfg = new LfgModule
            {
                Dao = _dao,
                CommandContext = _commandContext
            };
        }

        private DataTable CreateTestDataTable(int columns, int rows)
        {
            var datatable = new DataTable();
            for (var i = 0; i < columns; ++i)
            {
                datatable.Columns.Add($"Column{i + 1:D2}");
            }

            for (var i = 0; i < rows; ++i)
            {
                var rowNumber = i;
                var rowValues = Enumerable.Range(0, columns)
                    .Select(columnNumber => $"({rowNumber + 1:D2}, {columnNumber + 1:D2})")
                    .ToArray<object>();

                datatable.Rows.Add(rowValues);
            }

            return datatable;
        }

        [TestCase(DevRoleId, 1)]
        [TestCase(TesterRoleId, 1)]
        [TestCase(ModRoleId, 1)]
        [TestCase(UserRoleId, 0)]
        [TestCase(99u, 0)]
        public async Task OnlyAllowedUsersCanGetStats(ulong roleId, int expectedReceivedCalls)
        {
            _dao.GetGeneralStats().Returns(CreateTestDataTable(1, 0));
            var userRoles = new [] { roleId };
            _user.RoleIds.Returns(r => userRoles);

            await _lfg.Lfg("stats");

            await _dao.Received(expectedReceivedCalls).GetGeneralStats();

            if (expectedReceivedCalls == 0)
                await _dao.Received(0).InsertCommand(Arg.Any<ulong>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        }

        [Test]
        public async Task ReturnsAppropriateMessageWhenNoStatsAreAvailable()
        {
            _dao.GetGeneralStats().Returns(CreateTestDataTable(1, 0));
            
            await _lfg.Lfg("stats");

            await _channel.Received(1).SendMessageAsync(Arg.Is("```No stats available```"));
        }

        [TestCase(5, 32, 1)]
        [TestCase(5, 33, 2)]
        [TestCase(5, 64, 2)]
        [TestCase(5, 65, 3)]
        [TestCase(5, 99, 4)]
        public async Task SplitsTablesCorrectly(int columns, int rows, int expectedTables)
        {
            _dao.GetGeneralStats().Returns(CreateTestDataTable(columns, rows));
            var messages = new List<string>();
            await _channel.SendMessageAsync(Arg.Do<string>(m => messages.Add(m)));

            await _lfg.Lfg("stats");
            
            Assert.AreEqual(expectedTables, messages.Count);

            var maxRowCount = 32;
            var messageNumber = 1;
            foreach (var message in messages)
            {
                var lines = message.Split(Environment.NewLine).ToList();

                // Remove header and separator
                lines.RemoveRange(0, 2);

                // Remove Discord closing element
                lines.RemoveAt(lines.Count - 1);

                Assert.IsTrue(lines.Count <= maxRowCount);

                if (lines.Count == maxRowCount)
                {
                    Assert.IsTrue(lines.Last().StartsWith($"({messageNumber * maxRowCount:D2},"));
                }
                else
                {
                    Assert.IsTrue(lines.Last().StartsWith($"({(messageNumber - 1) * maxRowCount + lines.Count:D2},"));
                }
                messageNumber++;
            }
        }

        [Test]
        public void ThrowsExceptionForLargeResultSets()
        {
            _dao.GetGeneralStats().Returns(CreateTestDataTable(5, 350));
            var exception = Assert.ThrowsAsync<Exception>(async () => await _lfg.Lfg("stats"));
            Assert.IsTrue(exception.Message.StartsWith("Stats data estimation too big: "));
        }
    }
}