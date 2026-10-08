using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using TechTalk.SpecFlow;

namespace ServerMain.Tests.Steps
{
    public partial class Jokers100Steps
    {
        private double _kenoBet;
        private List<int> _kenoSel = new();

        [Given(@"玩家設定 Keno 下注為 (.*) 點")]
        public void GivenSetKenoBet(double bet)
        {
            _kenoBet = bet;
        }

        [Given(@"玩家嘗試選取 Keno 號碼 \[(.*)\]")]
        [When(@"玩家嘗試選取 Keno 號碼 \[(.*)\]")]
        public async Task WhenTrySelectKenoNumbers(string numbers)
        {
            if (_kenoBet <= 0)
            {
                throw new InvalidOperationException("請先在 Feature 檔案中設定 Keno 下注金額 (_kenoBet > 0)");
            }
            _kenoSel = numbers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                              .Select(int.Parse)
                              .ToList();

            var body = new { gid = _gid, bet = _kenoBet, sel = _kenoSel };
            using var req = new HttpRequestMessage(HttpMethod.Post, "/keno/spin");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _playerToken);
            req.Content = JsonContent(body);
            _lastResponse = await _http.SendAsync(req);
            _lastResponseJson = await _lastResponse.Content.ReadAsStringAsync();
            if (!_lastResponse.IsSuccessStatusCode)
            {
                try
                {
                    _lastError = JsonSerializer.Deserialize<ErrorResult>(_lastResponseJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch
                {
                    _lastError = null;
                }
            }
            else
            {
                _lastError = null;
            }
        }

        [Then(@"Keno 選號應該失敗且 HTTP 狀態碼為 (.*)")]
        public void ThenKenoShouldFail(int statusCode)
        {
            ((int)_lastResponse!.StatusCode).Should().Be(statusCode);
        }

        [When(@"使用 Keno Cheat 參數開獎 \[(.*)\] 進行 Spin")]
        public async Task WhenUseKenoCheat(string cheatStr)
        {
            var cheat = cheatStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(int.Parse)
                                .ToList();

            var body = new { gid = _gid, bet = _kenoBet, sel = _kenoSel, cheat = cheat };
            using var req = new HttpRequestMessage(HttpMethod.Post, "/keno/spin");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _playerToken);
            req.Content = JsonContent(body);
            _lastResponse = await _http.SendAsync(req);
            _lastResponseJson = await _lastResponse.Content.ReadAsStringAsync();
            _lastResponse.IsSuccessStatusCode.Should().BeTrue();
        }

        [Then(@"Keno 總贏分應該為 (.*)")]
        public void ThenKenoTotalWinShouldBe(double expectedWin)
        {
            using var doc = JsonDocument.Parse(_lastResponseJson!);
            var wins = GetProp(doc.RootElement, "wins");
            var pay = GetProp(wins, "pay").GetDouble();
            pay.Should().Be(expectedWin);
        }
    }
}
