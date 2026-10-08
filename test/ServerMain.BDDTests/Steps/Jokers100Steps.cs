using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using TechTalk.SpecFlow;

namespace ServerMain.Tests.Steps
{
    [Binding]
    public partial class Jokers100Steps
    {
        private readonly FeatureContext _featureContext;
        private readonly ScenarioContext _scenarioContext;

        public Jokers100Steps(FeatureContext featureContext, ScenarioContext scenarioContext)
        {
            _featureContext = featureContext;
            _scenarioContext = scenarioContext;
        }

        private HttpClient _http
        {
            get
            {
                if (_featureContext != null && _featureContext.TryGetValue("HttpClient", out HttpClient? client) && client != null)
                {
                    return client;
                }
                throw new InvalidOperationException("未找到專屬 HttpClient。請確認在 Feature Background 中已執行 '啟動 Golang 伺服器' 步驟。");
            }
        }

        private string? _playerToken;
        private string? _adminToken;
        private ulong _playerUid = 3;
        private ulong _cid = 1;
        private ulong _gid;
        private double _walletBeforeSpin;
        private double _currentBet;
        private int _currentSel;
        private HttpResponseMessage? _lastResponse;
        private string? _lastResponseJson;
        private SpinResult? _spinResult;
        private DoubleUpResult? _doubleUpResult;
        private ErrorResult? _lastError;
        private SpinAuditData? _lastSpinAudit;

        public static JsonElement GetProp(JsonElement el, string name)
        {
            if (el.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Element is not an object, but {el.ValueKind}");
            }
            if (el.TryGetProperty(name, out var val)) return val;
            foreach (var prop in el.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                    return prop.Value;
            }
            throw new KeyNotFoundException($"Property '{name}' not found");
        }

        private async Task<string> GetAdminTokenAsync()
        {
            if (!string.IsNullOrWhiteSpace(_adminToken))
            {
                return _adminToken;
            }
            var adminBody = new { email = "admin@example.org", secret = "0YBoaT" };
            var adminRes = await _http.PostAsync("/signin", JsonContent(adminBody));
            adminRes.IsSuccessStatusCode.Should().BeTrue();

            var adminJson = await adminRes.Content.ReadAsStringAsync();
            using var adminDoc = JsonDocument.Parse(adminJson);
            _adminToken = GetProp(adminDoc.RootElement, "access").GetString();
            return _adminToken!;
        }

        private async Task<double> GetClubBankAsync(ulong cid = 1)
        {
            var token = await GetAdminTokenAsync();
            using var req = new HttpRequestMessage(HttpMethod.Post, "/club/info");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent(new { cid = cid });

            var res = await _http.SendAsync(req);
            res.IsSuccessStatusCode.Should().BeTrue();

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return GetProp(doc.RootElement, "bank").GetDouble();
        }

        private async Task<double> GetClubFundAsync(ulong cid = 1)
        {
            var token = await GetAdminTokenAsync();
            using var req = new HttpRequestMessage(HttpMethod.Post, "/club/info");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent(new { cid = cid });

            var res = await _http.SendAsync(req);
            res.IsSuccessStatusCode.Should().BeTrue();

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return GetProp(doc.RootElement, "fund").GetDouble();
        }

        private async Task<double> GetPlayerWalletAsync(ulong cid = 1)
        {
            var token = await GetAdminTokenAsync();
            using var req = new HttpRequestMessage(HttpMethod.Post, "/prop/wallet/get");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent(new { cid = cid, uid = _playerUid });

            var res = await _http.SendAsync(req);
            res.IsSuccessStatusCode.Should().BeTrue();

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return GetProp(doc.RootElement, "wallet").GetDouble();
        }

        [Given(@"設定目標俱樂部 ID 為 (.*)")]
        [Given(@"玩家隸屬俱樂部 (.*)")]
        public void GivenTargetClubId(ulong cid)
        {
            _cid = cid;
        }

        [Given(@"玩家使用帳號 ""(.*)"" 密碼 ""(.*)"" 成功登入並取得授權 Token")]
        [Given(@"玩家成功登入並取得授權 Token")]
        public async Task GivenPlayerSignsIn(string email = "player@example.org", string secret = "iVI05M")
        {
            var body = new { email = email, secret = secret };
            var response = await _http.PostAsync("/signin", JsonContent(body));
            response.IsSuccessStatusCode.Should().BeTrue();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            _playerToken = GetProp(doc.RootElement, "access").GetString();
            _playerUid = GetProp(doc.RootElement, "uid").GetUInt64();

            _playerToken.Should().NotBeNullOrWhiteSpace();
        }

        [Given(@"玩家點數錢包充值至固定金額 (.*) 點")]
        [Given(@"玩家點數錢包餘額充值至固定金額 (.*) 點")]
        public async Task GivenPlayerWalletRechargedTo(double targetWallet)
        {
            var adminToken = await GetAdminTokenAsync();
            var currentWallet = await GetPlayerWalletAsync(_cid);

            while (Math.Abs(targetWallet - currentWallet) > 0.0001)
            {
                var diff = targetWallet - currentWallet;
                var step = Math.Sign(diff) * Math.Min(Math.Abs(diff), 100000.0);

                var refillBody = new { cid = _cid, uid = _playerUid, sum = step };
                using var req = new HttpRequestMessage(HttpMethod.Post, "/prop/wallet/add");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
                req.Content = JsonContent(refillBody);

                var refillRes = await _http.SendAsync(req);
                var refillContent = await refillRes.Content.ReadAsStringAsync();
                refillRes.IsSuccessStatusCode.Should().BeTrue(
                    $"refill failed with status {refillRes.StatusCode} and body {refillContent}, step={step}, current={currentWallet}");

                currentWallet = await GetPlayerWalletAsync(_cid);
            }

            currentWallet.Should().BeApproximately(targetWallet, 0.00001);
        }

        [Given(@"俱樂部 (.*) 水池充值至固定金額 (.*) 點")]
        [Given(@"俱樂部 (.*) 水池餘額充值至固定金額 (.*) 點")]
        public async Task GivenClubBankRechargedToWithCid(ulong cid, double targetBank)
        {
            _cid = cid;
            var token = await GetAdminTokenAsync();
            var currentBank = await GetClubBankAsync(cid);
            var diff = targetBank - currentBank;
            if (Math.Abs(diff) > 0.0001)
            {
                var cashinBody = new { cid = cid, banksum = diff };
                using var cashinReq = new HttpRequestMessage(HttpMethod.Post, "/club/cashin");
                cashinReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                cashinReq.Content = JsonContent(cashinBody);

                var cashinRes = await _http.SendAsync(cashinReq);
                cashinRes.IsSuccessStatusCode.Should().BeTrue();
            }

            var verifiedBank = await GetClubBankAsync(cid);
            verifiedBank.Should().BeApproximately(targetBank, 0.00001);
        }

        [Given(@"俱樂部水池充值至固定金額 (.*) 點")]
        [Given(@"俱樂部水池餘額充值至固定金額 (.*) 點")]
        public async Task GivenClubBankRechargedTo(double targetBank)
        {
            await GivenClubBankRechargedToWithCid(_cid, targetBank);
        }

        [Given(@"俱樂部 (.*) 彩金池充值至固定金額 (.*) 點")]
        [Given(@"俱樂部 (.*) 彩金池餘額充值至固定金額 (.*) 點")]
        public async Task GivenClubFundRechargedToWithCid(ulong cid, double targetFund)
        {
            _cid = cid;
            var token = await GetAdminTokenAsync();
            var currentFund = await GetClubFundAsync(cid);
            var diff = targetFund - currentFund;
            if (Math.Abs(diff) > 0.0001)
            {
                var cashinBody = new { cid = cid, fundsum = diff };
                using var cashinReq = new HttpRequestMessage(HttpMethod.Post, "/club/cashin");
                cashinReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                cashinReq.Content = JsonContent(cashinBody);

                var cashinRes = await _http.SendAsync(cashinReq);
                cashinRes.IsSuccessStatusCode.Should().BeTrue();
            }

            var verifiedFund = await GetClubFundAsync(cid);
            verifiedFund.Should().BeApproximately(targetFund, 0.00001);
        }

        [Given(@"俱樂部彩金池充值至固定金額 (.*) 點")]
        [Given(@"俱樂部彩金池餘額充值至固定金額 (.*) 點")]
        public async Task GivenClubFundRechargedTo(double targetFund)
        {
            await GivenClubFundRechargedToWithCid(_cid, targetFund);
        }

        [Given(@"玩家個人目標 RTP 設定為 (.*)")]
        [Given(@"設定玩家目標 RTP 為 (.*)")]
        public async Task GivenPlayerRtpSetTo(double targetRtp)
        {
            var adminToken = await GetAdminTokenAsync();
            var rtpBody = new { cid = _cid, uid = _playerUid, mrtp = targetRtp };
            using var req = new HttpRequestMessage(HttpMethod.Post, "/prop/rtp/set");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            req.Content = JsonContent(rtpBody);

            var res = await _http.SendAsync(req);
            res.IsSuccessStatusCode.Should().BeTrue();
        }

        [Given(@"建立 EGame\.(\w+) 遊戲場景")]
        public async Task GivenGameSceneCreated(string gameName)
        {
            var game = Enum.Parse<EGame>(gameName, true);
            var body = new { cid = _cid, uid = _playerUid, game = (uint)game };
            using var req = new HttpRequestMessage(HttpMethod.Post, "/game/new");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _playerToken);
            req.Content = JsonContent(body);

            var res = await _http.SendAsync(req);
            res.IsSuccessStatusCode.Should().BeTrue();

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            _gid = GetProp(doc.RootElement, "gid").GetUInt64();
            _walletBeforeSpin = GetProp(doc.RootElement, "wallet").GetDouble();
            _gid.Should().BeGreaterThan(0);
        }

        [Given(@"玩家設定單線下注為 (.*) 且選擇 (.*) 條押註線")]
        public void GivenPlayerSetsBetAndSel(double bet, int sel)
        {
            _currentBet = bet;
            _currentSel = sel;
        }

        private async Task HandleResponseContentAsync()
        {
            if (_lastResponse == null) return;
            _lastResponseJson = await _lastResponse.Content.ReadAsStringAsync();
            if (_lastResponse.IsSuccessStatusCode)
            {
                try
                {
                    _spinResult = JsonSerializer.Deserialize<SpinResult>(_lastResponseJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch
                {
                    _spinResult = null;
                }

                try
                {
                    _doubleUpResult = JsonSerializer.Deserialize<DoubleUpResult>(_lastResponseJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch
                {
                    _doubleUpResult = null;
                }

                _lastError = null;
            }
            else
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
                _spinResult = null;
                _doubleUpResult = null;
            }
        }

        [When(@"進行 Spin 旋轉")]
        public async Task WhenSpin()
        {
            if (_spinResult != null)
            {
                _walletBeforeSpin = _spinResult.Wallet;
            }

            var body = new
            {
                gid = _gid,
                bet = _currentBet,
                sel = _currentSel
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "/slot/spin");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _playerToken);
            req.Content = JsonContent(body);

            _lastResponse = await _http.SendAsync(req);
            await HandleResponseContentAsync();
        }

        [When(@"未提供授權 Token 進行 Spin")]
        public async Task WhenSpinWithoutToken()
        {
            var body = new
            {
                gid = _gid,
                bet = _currentBet,
                sel = _currentSel
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "/slot/spin");
            req.Content = JsonContent(body);

            _lastResponse = await _http.SendAsync(req);
            await HandleResponseContentAsync();
        }

        [When(@"使用不存在的 GID (.*) 進行 Spin")]
        public async Task WhenSpinWithNonExistentGid(ulong invalidGid)
        {
            var body = new
            {
                gid = invalidGid,
                bet = _currentBet,
                sel = _currentSel
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "/slot/spin");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _playerToken);
            req.Content = JsonContent(body);

            _lastResponse = await _http.SendAsync(req);
            await HandleResponseContentAsync();
        }

        [When(@"使用 Cheat 參數 \[(.*)\] 進行 Spin")]
        public async Task WhenSpinWithCheat(string cheatArrayStr)
        {
            if (_spinResult != null)
            {
                _walletBeforeSpin = _spinResult.Wallet;
            }

            var cheat = cheatArrayStr.Split(',')
                                     .Select(s => int.Parse(s.Trim()))
                                     .ToArray();

            var body = new
            {
                gid = _gid,
                bet = _currentBet,
                sel = _currentSel,
                cheat = cheat
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "/slot/spin");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _playerToken);
            req.Content = JsonContent(body);

            _lastResponse = await _http.SendAsync(req);
            await HandleResponseContentAsync();
        }

        [Then(@"Spin 回傳狀態應該成功")]
        public async Task ThenSpinStatusSuccess()
        {
            _lastResponse.Should().NotBeNull();
            if (!_lastResponse!.IsSuccessStatusCode)
            {
                var content = await _lastResponse.Content.ReadAsStringAsync();
                _lastResponse.IsSuccessStatusCode.Should().BeTrue($"API returned {_lastResponse.StatusCode}: {content}");
            }
            _spinResult.Should().NotBeNull();
        }

        [Then(@"盤面結果應該為:")]
        public void ThenGridShouldBe(Table table)
        {
            var actualGrid = _spinResult?.Game?.Grid;
            actualGrid.Should().NotBeNull("Spin 回傳結果中必須包含遊戲盤面 Grid");

            var allRows = new List<List<int>>();
            if (table.Header.All(h => int.TryParse(h.Trim(), out _)))
            {
                allRows.Add(table.Header.Select(h => int.Parse(h.Trim())).ToList());
            }

            foreach (var row in table.Rows)
            {
                allRows.Add(row.Values.Select(v => int.Parse(v.Trim())).ToList());
            }

            allRows.Count.Should().Be(actualGrid![0].Count, $"盤面列數應為 {actualGrid[0].Count}");

            for (int r = 0; r < allRows.Count; r++)
            {
                var rowValues = allRows[r];
                rowValues.Count.Should().Be(actualGrid.Count, $"盤面欄數應為 {actualGrid.Count}");
                for (int c = 0; c < rowValues.Count; c++)
                {
                    actualGrid[c][r].Should().Be(rowValues[c],
                        $"盤面座標 (軸 {c + 1}, 列 {r + 1}) 符號應該相符");
                }
            }
        }

        [Then(@"應該包含彩金 Jackpot 派彩")]
        public void ThenShouldContainJackpotWin()
        {
            _spinResult!.Wins.Should().Contain(w => w.Jid > 0);
        }

        [Then(@"Spin 回傳之彩金池餘額 JpFund 應該為 (.*) 點")]
        [Then(@"Spin 回傳之彩金池餘額 JpFund 應該為 (.*)")]
        public void ThenJpFundShouldBe(double expectedFund)
        {
            _spinResult.Should().NotBeNull();
            _spinResult!.JpFund.Should().BeApproximately(expectedFund, 0.00001);
        }

        [Then(@"盤面結果二維陣列應該為 (.*)")]
        public void ThenGridArrayShouldBe(string arrayJson)
        {
            var expected = JsonSerializer.Deserialize<List<List<int>>>(arrayJson);
            _spinResult?.Game?.Grid.Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
        }
        [Then(@"總贏分應該為 (.*)")]
        public void ThenTotalGainShouldBe(double expectedGain)
        {
            var totalGain = (_spinResult?.Wins ?? new List<WinItem>()).Sum(w => w.Pay * (w.Mp > 0 ? w.Mp : 1.0));
            totalGain.Should().Be(expectedGain);
            if (_spinResult?.Game != null && _spinResult.Game.Fsr == 0 && _spinResult.Game.Fsn == 0)
            {
                _spinResult.Game.Gain.Should().Be(expectedGain);
            }
        }

        [Then(@"總贏分應該大於 (.*)")]
        public void ThenTotalGainShouldBeGreaterThan(double threshold)
        {
            var totalGain = (_spinResult?.Wins ?? new List<WinItem>()).Sum(w => w.Pay * (w.Mp > 0 ? w.Mp : 1.0));
            totalGain.Should().BeGreaterThan(threshold);
            if (_spinResult?.Game != null)
            {
                _spinResult.Game.Gain.Should().BeGreaterThan(threshold);
            }
        }

        [Then(@"中獎線數應該為 (.*) 條")]
        public void ThenWinningLineCountShouldBe(int expectedCount)
        {
            _spinResult!.Wins.Should().HaveCount(expectedCount);
        }

        [Then(@"應該包含 Scatter 符號 (.*) 或 (.*) 的派彩")]
        public void ThenShouldContainScatterWinWithSyms(int sym1, int sym2)
        {
            _spinResult!.Wins.Should().Contain(w => w.Sym == sym1 || w.Sym == sym2);
        }

        [Then(@"應該包含 ID 為 (.*) 的幸運輪盤 Bonus 獎勵")]
        public void ThenShouldContainSpin9BonusWithId(int bid)
        {
            _spinResult.Should().NotBeNull();
            _spinResult!.Wins.Should().Contain(w => w.Bid == bid);
        }

        [Then(@"ID 為 (.*) 的輪盤獎勵旋轉次數應該為 (.*) 次")]
        public void ThenSpin9CountShouldBe(int bid, int count)
        {
            var bonusWin = _spinResult!.Wins.FirstOrDefault(w => w.Bid == bid);
            bonusWin.Should().NotBeNull($"中獎項目中應包含 ID 為 {bid} 的 Bonus 獎勵");
            bonusWin!.Bon.HasValue.Should().BeTrue();
            bonusWin.Bon!.Value.GetArrayLength().Should().Be(count);
        }

        [Then(@"應該包含 ID 為 (.*) 的大富翁 Bonus 獎勵")]
        public void ThenShouldContainMonopolyBonusWithId(int bid)
        {
            _spinResult.Should().NotBeNull();
            _spinResult!.Wins.Should().Contain(w => w.Bid == bid);
        }

        [Then(@"ID 為 (.*) 的大富翁走棋步數應該為 (.*) 步")]
        public void ThenMonopolyStepCountShouldBe(int bid, int count)
        {
            var bonusWin = _spinResult!.Wins.FirstOrDefault(w => w.Bid == bid);
            bonusWin.Should().NotBeNull($"中獎項目中應包含 ID 為 {bid} 的 Bonus 獎勵");
            bonusWin!.Bon.HasValue.Should().BeTrue();
            bonusWin.Bon!.Value.GetArrayLength().Should().Be(count);
        }

        private async Task PerformDoubleUpAsync(double mult, bool half, int cheat)
        {
            var body = new { gid = _gid, mult = mult, half = half, cheat = cheat };
            using var req = new HttpRequestMessage(HttpMethod.Post, "/slot/doubleup");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _playerToken);
            req.Content = JsonContent(body);

            _lastResponse = await _http.SendAsync(req);
            await HandleResponseContentAsync();
            if (_activeMultiPlayer != null && _doubleUpResult != null)
            {
                _activeMultiPlayer.WalletBeforeSpin = _doubleUpResult.Wallet;
            }
        }

        [When(@"發起倍率為 (.*) 的全額比倍 Double Up")]
        public async Task WhenPerformDoubleUpAll(double mult)
        {
            await PerformDoubleUpAsync(mult, false, 0);
        }

        [When(@"發起倍率為 (.*) 的半額比倍 Double Up")]
        public async Task WhenPerformDoubleUpHalf(double mult)
        {
            await PerformDoubleUpAsync(mult, true, 0);
        }

        [When(@"發起倍率為 (.*) 的全額比倍 Double Up 並指定 Cheat 為 (-?\d+)")]
        public async Task WhenPerformDoubleUpAllWithCheat(double mult, int cheat)
        {
            await PerformDoubleUpAsync(mult, false, cheat);
        }

        [When(@"(玩家一|玩家二)發起倍率為 (.*) 的全額比倍 Double Up 並指定 Cheat 為 (-?\d+)")]
        public async Task WhenSpecificPlayerPerformDoubleUpAllWithCheat(string playerName, double mult, int cheat)
        {
            SwitchContextToPlayer(playerName);
            await PerformDoubleUpAsync(mult, false, cheat);
        }

        [When(@"發起倍率為 (.*) 的全額比倍 Double Up 並使用 Cheat (贏|輸)")]
        public async Task WhenPerformDoubleUpAllWithCheatName(double mult, string outcome)
        {
            int cheat = outcome == "贏" ? 1 : -1;
            await PerformDoubleUpAsync(mult, false, cheat);
        }

        [When(@"(玩家一|玩家二)發起倍率為 (.*) 的全額比倍 Double Up 並使用 Cheat (贏|輸)")]
        public async Task WhenSpecificPlayerPerformDoubleUpAllWithCheatName(string playerName, double mult, string outcome)
        {
            SwitchContextToPlayer(playerName);
            int cheat = outcome == "贏" ? 1 : -1;
            await PerformDoubleUpAsync(mult, false, cheat);
        }

        [Then(@"比倍回傳狀態應該成功")]
        public async Task ThenDoubleUpStatusSuccess()
        {
            _lastResponse.Should().NotBeNull();
            if (!_lastResponse!.IsSuccessStatusCode)
            {
                var content = await _lastResponse.Content.ReadAsStringAsync();
                _lastResponse.IsSuccessStatusCode.Should().BeTrue($"比倍 API 返回錯誤 {_lastResponse.StatusCode}: {content}");
            }
            _doubleUpResult.Should().NotBeNull("比倍結果不可為空");
        }

        [Then(@"比倍結果的贏分 Gain 應該為 (.*)")]
        public void ThenDoubleUpGainShouldBe(double expectedGain)
        {
            _doubleUpResult.Should().NotBeNull();
            _doubleUpResult!.Gain.Should().BeApproximately(expectedGain, 0.001);
        }

        [Then(@"比倍結果的下注風險 Risk 應該為 (.*)")]
        public void ThenDoubleUpRiskShouldBe(double expectedRisk)
        {
            _doubleUpResult.Should().NotBeNull();
            _doubleUpResult!.Risk.Should().BeApproximately(expectedRisk, 0.001);
        }

        [Then(@"比倍結果的中獎狀態 Win 應該為 (true|false)")]
        public void ThenDoubleUpWinShouldBe(bool expectedWin)
        {
            _doubleUpResult.Should().NotBeNull();
            _doubleUpResult!.Win.Should().Be(expectedWin);
        }

        [Then(@"比倍應該失敗且 HTTP 狀態碼為 (.*)")]
        public void ThenDoubleUpShouldFailWithStatusCode(int expectedStatusCode)
        {
            _lastResponse.Should().NotBeNull();
            ((int)_lastResponse!.StatusCode).Should().Be(expectedStatusCode);
        }

        [Then(@"比倍錯誤訊息應該包含 ""(.*)""")]
        public void ThenDoubleUpErrorMessageShouldContain(string expectedSubstring)
        {
            _lastResponse.Should().NotBeNull();
            var content = _lastError?.What ?? _lastResponseJson ?? string.Empty;
            content.Should().Contain(expectedSubstring);
        }

        [Then(@"未中獎且錢包扣款 (.*) 點")]
        public void ThenWalletDeductedBy(double cost)
        {
            _spinResult!.Wins.Should().BeEmpty();
            _spinResult.Wallet.Should().Be(_walletBeforeSpin - cost);
        }

        [Then(@"俱樂部水池餘額應該為 (.*) 點")]
        [Then(@"俱樂部水池餘額應該為 (.*)")]
        public async Task ThenClubBankShouldBe(double expectedBank)
        {
            var actualServerBank = await GetClubBankAsync(1);
            actualServerBank.Should().BeApproximately(expectedBank, 0.00001,
                "伺服器端俱樂部實際水池餘額應與預期相符");
        }

        [Then(@"玩家點數錢包餘額應該為 (.*) 點")]
        [Then(@"玩家點數錢包餘額應該為 (.*)")]
        [Then(@"玩家錢包餘額應該為 (.*) 點")]
        [Then(@"玩家錢包餘額應該為 (.*)")]
        public async Task ThenPlayerWalletShouldBe(double expectedWallet)
        {
            if (_spinResult != null)
            {
                _spinResult.Wallet.Should().BeApproximately(expectedWallet, 0.00001,
                    "Spin 回傳之錢包餘額應與預期相符");
            }

            var actualServerWallet = await GetPlayerWalletAsync(1);
            actualServerWallet.Should().BeApproximately(expectedWallet, 0.00001,
                "伺服器端玩家實際錢包餘額應與預期相符");
        }

        [Then(@"Spin 應該失敗且 HTTP 狀態碼為 (.*)")]
        public void ThenSpinShouldFailWithStatusCode(int expectedStatusCode)
        {
            _lastResponse.Should().NotBeNull();
            ((int)_lastResponse!.StatusCode).Should().Be(expectedStatusCode);
        }

        [Then(@"Spin 錯誤訊息應該包含 ""(.*)""")]
        public void ThenErrorMessageShouldContain(string expectedSubstr)
        {
            _lastResponse.Should().NotBeNull();
            var content = _lastError?.What ?? _lastResponseJson ?? string.Empty;
            content.Should().Contain(expectedSubstr);
        }

        [When(@"透過後台管理 API 查詢本次旋轉審計數據")]
        [When(@"透過後台 API 查詢本次旋轉審計數據")]
        public async Task WhenFetchSpinAuditData()
        {
            var adminToken = await GetAdminTokenAsync();
            using var req = new HttpRequestMessage(HttpMethod.Post, "/slot/admin/spin/info");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            req.Content = JsonContent(new { gid = _gid, sid = _spinResult?.Sid ?? 0 });

            var res = await _http.SendAsync(req);
            var content = await res.Content.ReadAsStringAsync();
            res.IsSuccessStatusCode.Should().BeTrue($"Admin spin info returned {res.StatusCode}: {content}");

            _lastSpinAudit = JsonSerializer.Deserialize<SpinAuditData>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            _lastSpinAudit.Should().NotBeNull();
        }

        [Then(@"達成水池風控嘗試次數 Attempts 應該為 (.*)")]
        public void ThenAttemptsShouldBe(int expectedAttempts)
        {
            _lastSpinAudit.Should().NotBeNull();
            _lastSpinAudit!.Attempts.Should().Be(expectedAttempts);
        }

        [Then(@"目標 RTP TargetRTP 應該為 (.*)")]
        public void ThenTargetRtpShouldBe(double expectedTargetRtp)
        {
            _lastSpinAudit.Should().NotBeNull();
            _lastSpinAudit!.TargetRTP.Should().BeApproximately(expectedTargetRtp, 0.0001);
        }

        [Then(@"輪帶有效 RTP EffRTP 應該為 (.*)")]
        public void ThenEffRtpShouldBe(double expectedEffRtp)
        {
            _lastSpinAudit.Should().NotBeNull();
            _lastSpinAudit!.EffRTP.Should().BeApproximately(expectedEffRtp, 0.000001);
        }

        [Then(@"旋轉前彩金池金額 FundBefore 應該為 (.*) 點")]
        [Then(@"旋轉前彩金池金額 FundBefore 應該為 (.*)")]
        public void ThenFundBeforeShouldBe(double expectedFund)
        {
            _lastSpinAudit.Should().NotBeNull();
            _lastSpinAudit!.FundBefore.Should().BeApproximately(expectedFund, 0.00001);
        }

        [Then(@"旋轉結算後彩金池金額 FundAfter 應該為 (.*) 點")]
        [Then(@"旋轉結算後最新彩金池金額 FundAfter 應該為 (.*) 點")]
        [Then(@"旋轉結算後彩金池金額 FundAfter 應該為 (.*)")]
        [Then(@"旋轉結算後最新彩金池金額 FundAfter 應該為 (.*)")]
        public void ThenFundAfterShouldBe(double expectedFund)
        {
            _lastSpinAudit.Should().NotBeNull();
            _lastSpinAudit!.FundAfter.Should().BeApproximately(expectedFund, 0.00001);
        }

        [Then(@"本次中彩金金額 JackWin 應該為 (.*) 點")]
        [Then(@"本次中彩金金額 JackWin 應該為 (.*)")]
        public void ThenJackWinShouldBe(double expectedJackWin)
        {
            _lastSpinAudit.Should().NotBeNull();
            _lastSpinAudit!.JackWin.Should().BeApproximately(expectedJackWin, 0.00001);
        }

        [Then(@"本次中彩金金額 JackWin 應該大於 (.*)")]
        public void ThenJackWinShouldBeGreaterThan(double minJackWin)
        {
            _lastSpinAudit.Should().NotBeNull();
            _lastSpinAudit!.JackWin.Should().BeGreaterThan(minJackWin);
        }

        [Then(@"彩金抽水比率 JackRate 應該為 (.*)")]
        public void ThenJackRateShouldBe(double expectedJackRate)
        {
            _lastSpinAudit.Should().NotBeNull();
            _lastSpinAudit!.JackRate.Should().BeApproximately(expectedJackRate, 0.00001);
        }

        [Then(@"本次抽水注入彩金池金額 JackRent 應該為 (.*) 點")]
        [Then(@"本次抽水注入彩金池金額 JackRent 應該為 (.*)")]
        public void ThenJackRentShouldBe(double expectedJackRent)
        {
            _lastSpinAudit.Should().NotBeNull();
            _lastSpinAudit!.JackRent.Should().BeApproximately(expectedJackRent, 0.00001);
        }

        [Then(@"輪帶序列 Reels 應該包含 (.*) 軸且長度分別為 \[(.*)\]")]
        public void ThenReelsLengthsShouldBe(int expectedReelCount, string lengthsStr)
        {
            _lastSpinAudit.Should().NotBeNull();
            _lastSpinAudit!.Reels.Should().HaveCount(expectedReelCount);
            var expectedLengths = lengthsStr.Split(',')
                                            .Select(s => int.Parse(s.Trim()))
                                            .ToList();
            _lastSpinAudit.Reels.Select(r => r.Count).Should().Equal(expectedLengths);
        }

        [Then(@"輪帶序列 Reels 軸數應該為 (.*)")]
        public void ThenReelsCountShouldBe(int expectedReelCount)
        {
            _lastSpinAudit.Should().NotBeNull();
            _lastSpinAudit!.Reels.Should().HaveCount(expectedReelCount);
        }

        private static StringContent JsonContent(object obj)
        {
            return new StringContent(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");
        }

        public class SpinResult
        {
            public ulong Sid { get; set; }
            public double Wallet { get; set; }
            public double JpFund { get; set; }
            private List<WinItem> _wins = new();
            public List<WinItem> Wins
            {
                get => _wins;
                set => _wins = value ?? new();
            }
            public SpinGame? Game { get; set; }
        }

        public class SpinGame
        {
            private List<List<int>> _grid = new();
            public List<List<int>> Grid
            {
                get => _grid;
                set => _grid = value ?? new();
            }

            private List<List<int>> _hits = new();
            public List<List<int>> Hits
            {
                get => _hits;
                set => _hits = value ?? new();
            }
            public int Cfn { get; set; }
            public double Gain { get; set; }
            public int Fsr { get; set; }
            public int Fsn { get; set; }
            public int Es { get; set; }
        }

        public class WinItem
        {
            public double Pay { get; set; }
            public double Mp { get; set; } = 1.0;
            public int Sym { get; set; }
            public int Num { get; set; }
            public int Li { get; set; }
            public int Fs { get; set; }
            public int Jid { get; set; }
            public int Bid { get; set; }
            public JsonElement? Bon { get; set; }
        }

        public class SpinAuditData
        {
            public int Attempts { get; set; }
            public double TargetRTP { get; set; }
            public double EffRTP { get; set; }
            public double FundBefore { get; set; }
            public double FundAfter { get; set; }
            public double JackWin { get; set; }
            public double JackRate { get; set; }
            public double JackRent { get; set; }
            public List<List<int>> Reels { get; set; } = new();
        }

        public class ErrorResult
        {
            public string What { get; set; } = string.Empty;
        }

        public class DoubleUpResult
        {
            public ulong Id { get; set; }
            public bool Win { get; set; }
            public double Risk { get; set; }
            public double Gain { get; set; }
            public double Wallet { get; set; }
        }
    }
}
