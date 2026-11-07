using MainCore.UI.Models;

namespace MainCore.Test
{
    public class ServerUrlTests
    {
        [Theory]
        [InlineData("https://ts1.x1.international.travian.com", "https://ts1.x1.international.travian.com")]
        [InlineData("ts1.x1.international.travian.com", "https://ts1.x1.international.travian.com")]
        [InlineData(" https://ts1.x1.international.travian.com/dorf1.php ", "https://ts1.x1.international.travian.com")]
        [InlineData("http://ts1.x1.international.travian.com", "http://ts1.x1.international.travian.com")]
        [InlineData("", "")]
        [InlineData("https://", "")]
        public void AcceptsAHostWithOrWithoutScheme(string input, string expected)
        {
            Assert.Equal(expected, input.GetServerUrl());
        }
    }
}
