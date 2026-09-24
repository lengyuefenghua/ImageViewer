using ImageViewer.Standalone;
using Xunit;

namespace ImageViewer.Tests.Standalone
{
    public sealed class ElevatedCommandTests
    {
        [Fact]
        public void Parse_returns_register_for_the_register_argument()
        {
            Assert.Equal(ElevatedAction.Register, ElevatedCommand.Parse(new[] { "--register-file-associations" }));
        }

        [Fact]
        public void Parse_returns_unregister_for_the_unregister_argument()
        {
            Assert.Equal(ElevatedAction.Unregister, ElevatedCommand.Parse(new[] { "--unregister-file-associations" }));
        }

        [Fact]
        public void Parse_is_case_insensitive_and_scans_all_arguments()
        {
            Assert.Equal(ElevatedAction.Register, ElevatedCommand.Parse(new[] { "C:\\a.jpg", "--REGISTER-FILE-ASSOCIATIONS" }));
        }

        [Fact]
        public void Parse_returns_none_for_no_or_unrelated_arguments()
        {
            Assert.Equal(ElevatedAction.None, ElevatedCommand.Parse(null));
            Assert.Equal(ElevatedAction.None, ElevatedCommand.Parse(new string[0]));
            Assert.Equal(ElevatedAction.None, ElevatedCommand.Parse(new[] { "C:\\pictures\\a.png" }));
        }
    }
}
