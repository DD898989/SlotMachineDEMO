using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using TechTalk.SpecFlow;

namespace ServerMain.Tests.Steps
{
    public partial class Jokers100Steps
    {
        private class MultiPlayerInfo
        {
            public ulong Uid { get; set; }
            public string Email { get; set; }
            public string Secret { get; set; }
            public string? Token { get; set; }
            public ulong Gid { get; set; }
            public double Bet { get; set; }
            public int Sel { get; set; }
            public double WalletBeforeSpin { get; set; }
            public SpinResult? LastSpinResult { get; set; }

            public MultiPlayerInfo(ulong uid, string email, string secret)
            {
                Uid = uid;
                Email = email;
                Secret = secret;
            }
        }

        private readonly Dictionary<string, MultiPlayerInfo> _multiPlayerDict = new();

        private MultiPlayerInfo GetOrCreatePlayer(string playerName)
        {
            if (!_multiPlayerDict.TryGetValue(playerName, out var p))
            {
                if (playerName == "玩家一")
                {
                    p = new MultiPlayerInfo(3, "player@example.org", "iVI05M");
                }
                else if (playerName == "玩家二")
                {
                    p = new MultiPlayerInfo(4, "player2@example.org", "iVI05M");
                }
                else
                {
                    throw new ArgumentException("未知的玩家名稱: " + playerName);
                }
                _multiPlayerDict[playerName] = p;
            }
            return p;
        }

        private MultiPlayerInfo? _activeMultiPlayer;

        private void SwitchContextToPlayer(string playerName)
        {
            var p = GetOrCreatePlayer(playerName);

            // 儲存先前活躍玩家狀態
            if (_activeMultiPlayer != null)
            {
                _activeMultiPlayer.Token = _playerToken;
                _activeMultiPlayer.Uid = _playerUid;
                _activeMultiPlayer.Gid = _gid;
                _activeMultiPlayer.Bet = _currentBet;
                _activeMultiPlayer.Sel = _currentSel;
                _activeMultiPlayer.WalletBeforeSpin = _walletBeforeSpin;
                _activeMultiPlayer.LastSpinResult = _spinResult;
            }

            _activeMultiPlayer = p;
            _playerToken = p.Token;
            _playerUid = p.Uid;
            _gid = p.Gid;
            _currentBet = p.Bet;
            _currentSel = p.Sel;
            _walletBeforeSpin = p.WalletBeforeSpin;
            _spinResult = p.LastSpinResult;
        }

        [Given(@"(玩家一|玩家二)使用帳號 ""(.*)"" 密碼 ""(.*)"" 成功登入並取得授權 Token")]
        public async Task GivenSpecificPlayerSignsInWithCredentials(string playerName, string email, string secret)
        {
            var p = GetOrCreatePlayer(playerName);
            p.Email = email;
            p.Secret = secret;
            await GivenSpecificPlayerSignsIn(playerName);
        }

        [Given(@"(玩家一|玩家二)成功登入並取得授權 Token")]
        public async Task GivenSpecificPlayerSignsIn(string playerName)
        {
            var p = GetOrCreatePlayer(playerName);
            var body = new { email = p.Email, secret = p.Secret };
            var response = await _http.PostAsync("/signin", JsonContent(body));
            response.IsSuccessStatusCode.Should().BeTrue($"玩家 {playerName} 登入失敗: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            p.Token = GetProp(doc.RootElement, "access").GetString();
            p.Uid = GetProp(doc.RootElement, "uid").GetUInt64();
            p.Token.Should().NotBeNullOrWhiteSpace();

            SwitchContextToPlayer(playerName);
        }

        [Given(@"(玩家一|玩家二)點數錢包充值至固定金額 (.*) 點")]
        [Given(@"(玩家一|玩家二)點數錢包餘額充值至固定金額 (.*) 點")]
        public async Task GivenSpecificPlayerWalletRechargedTo(string playerName, double targetWallet)
        {
            SwitchContextToPlayer(playerName);
            await GivenPlayerWalletRechargedTo(targetWallet);
        }

        [Given(@"(玩家一|玩家二)個人目標 RTP 設定為 (.*)")]
        public async Task GivenSpecificPlayerRtpSetTo(string playerName, double targetRtp)
        {
            SwitchContextToPlayer(playerName);
            await GivenPlayerRtpSetTo(targetRtp);
        }

        [Given(@"(玩家一|玩家二)建立 EGame\.(\w+) 遊戲場景")]
        public async Task GivenSpecificPlayerGameSceneCreated(string playerName, string gameName)
        {
            SwitchContextToPlayer(playerName);
            await GivenGameSceneCreated(gameName);
            _multiPlayerDict[playerName].Gid = _gid;
        }

        [Given(@"(玩家一|玩家二)設定單線下注為 (.*) 且選擇 (.*) 條押註線")]
        public void GivenSpecificPlayerSetsBetAndSel(string playerName, double bet, int sel)
        {
            SwitchContextToPlayer(playerName);
            GivenPlayerSetsBetAndSel(bet, sel);
            _multiPlayerDict[playerName].Bet = bet;
            _multiPlayerDict[playerName].Sel = sel;
        }

        [When(@"切換至(玩家一|玩家二)")]
        [Given(@"切換至(玩家一|玩家二)")]
        public void StepSwitchToPlayer(string playerName)
        {
            SwitchContextToPlayer(playerName);
        }

        [When(@"(玩家一|玩家二)使用 Cheat 參數 \[(.*)\] 進行 Spin")]
        public async Task WhenSpecificPlayerSpinWithCheat(string playerName, string cheatArrayStr)
        {
            SwitchContextToPlayer(playerName);
            if (_spinResult == null)
            {
                _walletBeforeSpin = await GetPlayerWalletAsync(1);
            }
            await WhenSpinWithCheat(cheatArrayStr);
            _multiPlayerDict[playerName].LastSpinResult = _spinResult;
            _multiPlayerDict[playerName].WalletBeforeSpin = _walletBeforeSpin;
        }

        [Then(@"(玩家一|玩家二)點數錢包餘額應該為 (.*) 點")]
        [Then(@"(玩家一|玩家二)點數錢包餘額應該為 (.*)")]
        [Then(@"(玩家一|玩家二)錢包餘額應該為 (.*) 點")]
        public async Task ThenSpecificPlayerWalletShouldBe(string playerName, double expectedWallet)
        {
            var p = _multiPlayerDict[playerName];
            var token = await GetAdminTokenAsync();
            using var req = new HttpRequestMessage(HttpMethod.Post, "/prop/wallet/get");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent(new { cid = (ulong)1, uid = p.Uid });

            var res = await _http.SendAsync(req);
            res.IsSuccessStatusCode.Should().BeTrue();

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var actualWallet = GetProp(doc.RootElement, "wallet").GetDouble();

            actualWallet.Should().BeApproximately(expectedWallet, 0.00001,
                $"伺服器端 {playerName} 實際錢包餘額應與預期相符");
        }
    }
}
