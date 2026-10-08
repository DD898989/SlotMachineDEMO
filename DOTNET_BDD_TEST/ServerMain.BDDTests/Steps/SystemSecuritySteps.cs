using System.Net.Http.Headers;
using FluentAssertions;
using TechTalk.SpecFlow;

namespace ServerMain.Tests.Steps
{
    public partial class Jokers100Steps
    {
        private double _initialTotalAssets;

        [Given(@"系統初始設定準備完成")]
        public void GivenSystemIsReady()
        {
        }

        [Given(@"記錄初始資產總和 \(錢包 \+ 水池 \+ 彩金池\)")]
        public async Task GivenRecordInitialAssets()
        {
            var wallet = await GetPlayerWalletAsync(1);
            var bank = await GetClubBankAsync(1);
            var fund = await GetClubFundAsync(1);
            _initialTotalAssets = wallet + bank + fund;
        }

        [When(@"進行 (.*) 次隨機旋轉")]
        public async Task WhenSpinRandomlyMultipleTimes(int times)
        {
            for (int i = 0; i < times; i++)
            {
                await WhenSpin();
                _lastResponse!.IsSuccessStatusCode.Should().BeTrue();
            }
        }

        [Then(@"最終資產總和必須等於初始資產總和 \(零和校驗通過\)")]
        public async Task ThenZeroSumValidationPasses()
        {
            var wallet = await GetPlayerWalletAsync(1);
            var bank = await GetClubBankAsync(1);
            var fund = await GetClubFundAsync(1);
            var finalTotal = wallet + bank + fund;
            Math.Abs(finalTotal - _initialTotalAssets).Should().BeLessThan(0.00001);
        }

        [Given(@"玩家嘗試充值單筆 (.*) 點")]
        public async Task GivenPlayerTriesToDeposit(double sum)
        {
            var token = await GetAdminTokenAsync();
            using var req = new HttpRequestMessage(HttpMethod.Post, "/prop/wallet/add");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent(new { cid = 1, uid = _playerUid, sum = sum });
            _lastResponse = await _http.SendAsync(req);
            await HandleResponseContentAsync();
        }

        [Then(@"充值應被拒絕且 HTTP 狀態碼為 (.*)")]
        public void ThenDepositShouldBeRejected(int statusCode)
        {
            ((int)_lastResponse!.StatusCode).Should().Be(statusCode);
        }

        [Given(@"玩家隸屬俱樂部 (.*) 且水池設為 (.*) 點")]
        [Given(@"系統存在俱樂部 (.*) 且水池設為 (.*) 點")]
        public async Task GivenSetupClubBank(ulong cid, double targetBank)
        {
            var token = await GetAdminTokenAsync();
            var currentBank = await GetClubBankAsync(cid);
            var diff = targetBank - currentBank;
            if (Math.Abs(diff) > 0.001)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "/club/cashin");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                req.Content = JsonContent(new { cid = cid, banksum = diff });
                var res = await _http.SendAsync(req);
                res.IsSuccessStatusCode.Should().BeTrue();
            }
        }

        [Then(@"俱樂部 (.*) 水池餘額應該為 (.*)")]
        [Then(@"俱樂部 (.*) 水池餘額應該為 (.*) 點")]
        [Then(@"俱樂部 (.*) 水池餘額應保持精確的 (.*) 點")]
        public async Task ThenClubBankShouldBe(ulong cid, double expected)
        {
            var bank = await GetClubBankAsync(cid);
            bank.Should().BeApproximately(expected, 0.05);
        }

        [Then(@"輪帶有效 RTP 應隨之調整對齊為 (.*) \(容差 (.*)\)")]
        public void ThenEffectiveRtpAdjustedWithExpected(double expectedRtp, double tolerance)
        {
            _lastSpinAudit.Should().NotBeNull();
            _lastSpinAudit!.EffRTP.Should().BeApproximately(expectedRtp, tolerance);
        }

        [When(@"玩家使用一般會員 Token 嘗試呼叫俱樂部 (.*) 注資 API 充值 (.*) 點")]
        public async Task WhenPlayerCallsCashinWithArgs(ulong cid, double banksum)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/club/cashin");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _playerToken);
            req.Content = JsonContent(new { cid = cid, banksum = banksum });
            _lastResponse = await _http.SendAsync(req);
            await HandleResponseContentAsync();
        }

        [Then(@"操作應被拒絕且 HTTP 狀態碼為 (.*)")]
        public void ThenOperationRejected(int statusCode)
        {
            ((int)_lastResponse!.StatusCode).Should().Be(statusCode);
        }
    }
}
