using System.Net.Http.Headers;
using FluentAssertions;
using TechTalk.SpecFlow;

namespace ServerMain.Tests.Steps
{
    public partial class Jokers100Steps
    {
        [Given(@"玩家設定單線下注為 (.*) 且使用固定 (.*) 條押註線")]
        public void GivenPlayerSetsBetWithDefaultLines(double bet, int fixedLines)
        {
            _currentBet = bet;
            _currentSel = 0;
        }

        [When(@"進行連鎖掉落 Spin 旋轉")]
        public async Task WhenCascadeFallSpin()
        {
            await WhenSpin();
        }

        [When(@"嘗試變更押註線數為 (.*) 條")]
        public async Task WhenTryChangeSel(int newSel)
        {
            var body = new { gid = _gid, sel = newSel };
            using var req = new HttpRequestMessage(HttpMethod.Post, "/slot/sel/set");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _playerToken);
            req.Content = JsonContent(body);

            _lastResponse = await _http.SendAsync(req);
            await HandleResponseContentAsync();
        }

        [Then(@"設定押註線數應該失敗且 HTTP 狀態碼為 (.*)")]
        public void ThenSetSelShouldFailWithStatusCode(int expectedStatusCode)
        {
            _lastResponse.Should().NotBeNull();
            ((int)_lastResponse!.StatusCode).Should().Be(expectedStatusCode);
        }

        [Then(@"應該包含 ID 為 (.*) 的 Wild 百搭符號替代派彩")]
        public void ThenShouldContainWildWinWithId(int wildSym)
        {
            _spinResult.Should().NotBeNull();
            _spinResult!.Game.Should().NotBeNull();
            var grid = _spinResult.Game!.Grid;
            bool containsWild = grid.Any(col => col.Contains(wildSym));
            containsWild.Should().BeTrue($"盤面上應該出現 Wild 百搭符號 ({wildSym})");
            _spinResult.Wins.Should().NotBeEmpty("應該有命中連線中獎");
        }

        [Then(@"應該包含 ID 為 (.*) 的 Scatter 符號 (.*) 個且獲得 (.*) 次 Free Fall 免費旋轉")]
        public void ThenShouldContainGonzoScatterWinWithId(int sym, int count, int expectedFs)
        {
            _spinResult.Should().NotBeNull();
            _spinResult!.Wins.Should().Contain(w => w.Sym == sym && w.Num == count && w.Fs == expectedFs,
                $"中獎項目中應包含 {count} 個 Scatter (符號 {sym}) 且獲得 {expectedFs} 次 Free Fall 免費旋轉");
        }

        [Then(@"掉落連消次數 CFN 應該為 (.*)")]
        public void ThenCfnShouldBe(int expectedCfn)
        {
            _spinResult?.Game.Should().NotBeNull();
            _spinResult!.Game!.Cfn.Should().Be(expectedCfn, $"掉落連消次數 CFN 應為 {expectedCfn}");
        }

        [Then(@"盤面應標記需要消除掉落的符號 Hits")]
        public void ThenGridShouldHaveHits()
        {
            _spinResult?.Game.Should().NotBeNull();
            var hits = _spinResult!.Game!.Hits;
            hits.Should().NotBeNull();
            var totalHits = hits.Sum(col => col.Sum(v => v));
            totalHits.Should().BeGreaterThan(0, "盤面上應有標記大於 0 的消除掉落命中 Hits");
        }

        [Then(@"盤面不應標記任何需要消除掉落的符號 Hits")]
        public void ThenGridShouldNotHaveHits()
        {
            _spinResult?.Game.Should().NotBeNull();
            var hits = _spinResult!.Game!.Hits;
            if (hits != null && hits.Count > 0)
            {
                var totalHits = hits.Sum(col => col.Sum(v => v));
                totalHits.Should().Be(0, "未中獎或已結束掉落時，Hits 標記總和應為 0");
            }
        }

        [Then(@"中獎項目應該包含乘倍為 (.*) 的連線")]
        public void ThenWinsShouldContainMultiplier(double expectedMp)
        {
            _spinResult.Should().NotBeNull();
            _spinResult!.Wins.Should().Contain(w => Math.Abs(w.Mp - expectedMp) < 0.001,
                $"中獎項目應包含乘倍 MP 為 {expectedMp} 的連線");
        }
    }
}
