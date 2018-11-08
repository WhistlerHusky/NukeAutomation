using Shouldly;
using Xunit;

namespace NukeAutomation_Test
{
    public class Nuke_Automation_Test
    {
        [Theory]
        [InlineData(1,1)]
        [InlineData(2,2)]
        public void ShouldBeTheSame(int a, int b)
        {
            a.ShouldBe(b);
        }
    }
}
