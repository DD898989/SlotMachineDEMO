using FluentAssertions;
using TechTalk.SpecFlow;

namespace ServerMain.Tests.Steps
{
    public partial class Jokers100Steps
    {
        [Then(@"應該包含 ID 為 (.*) 的 Scatter 符號 (.*) 個且獲得 (.*) 次免費旋轉")]
        public void ThenShouldContainScatterWinWithSymCountAndFreeSpins(int sym, int count, int expectedFs)
        {
            _spinResult.Should().NotBeNull();
            _spinResult!.Wins.Should().Contain(w => w.Sym == sym && w.Num == count && w.Fs == expectedFs,
                $"中獎項目中應包含 {count} 個 Scatter (符號 {sym}) 且獲得 {expectedFs} 次免費旋轉");
        }

        [Then(@"中獎項目應該包含 ID 為 (.*) 的 Scatter 符號且獲得 (.*) 次免費旋轉")]
        public void ThenShouldContainScatterWinWithSymAndFreeSpins(int sym, int expectedFs)
        {
            _spinResult.Should().NotBeNull();
            _spinResult!.Wins.Should().Contain(w => w.Sym == sym && w.Fs == expectedFs,
                $"中獎項目中應包含獲得 {expectedFs} 次免費旋轉的 Scatter 符號 (符號 {sym})");
        }

        [Then(@"遊戲應該處於 FreeMode 免費模式")]
        public void ThenGameShouldBeInFreeMode()
        {
            _spinResult?.Game.Should().NotBeNull();
            _spinResult!.Game!.Fsr.Should().BeGreaterThan(0, "遊戲應處於免費旋轉 FreeMode 狀態 (FSR > 0)");
        }

        [Then(@"遊戲不應處於 FreeMode 免費模式")]
        public void ThenGameShouldNotInFreeMode()
        {
            _spinResult?.Game.Should().NotBeNull();
            _spinResult!.Game!.Fsr.Should().Be(0, "遊戲應結束 FreeMode 狀態 (FSR == 0)");
        }

        [Then(@"剩餘免費旋轉次數 FSR 應該為 (.*)")]
        [Then(@"剩餘免費旋轉次數 FSR 應該為 (.*) 次")]
        public void ThenFsrShouldBe(int expectedFsr)
        {
            _spinResult?.Game.Should().NotBeNull();
            _spinResult!.Game!.Fsr.Should().Be(expectedFsr, $"剩餘免費旋轉次數 FSR 應為 {expectedFsr}");
        }

        [Then(@"已完成免費旋轉次數 FSN 應該為 (.*)")]
        [Then(@"已完成免費旋轉次數 FSN 應該為 (.*) 次")]
        public void ThenFsnShouldBe(int expectedFsn)
        {
            _spinResult?.Game.Should().NotBeNull();
            _spinResult!.Game!.Fsn.Should().Be(expectedFsn, $"已完成免費旋轉次數 FSN 應為 {expectedFsn}");
        }

        [Then(@"特殊擴展符號 ES 應該已被選定且範圍在 (.*) 到 (.*) 之間")]
        public void ThenExpandingSymbolShouldBeSelected(int min, int max)
        {
            _spinResult?.Game.Should().NotBeNull();
            _spinResult!.Game!.Es.Should().BeInRange(min, max, $"進入 FreeMode 應隨機選定 {min}~{max} 號符號作為特殊擴展符號 ES");
        }

        [Then(@"特殊擴展符號 ES 應該已被清除")]
        public void ThenExpandingSymbolShouldBeCleared()
        {
            _spinResult?.Game.Should().NotBeNull();
            _spinResult!.Game!.Es.Should().Be(0, "離開 FreeMode 後特殊擴展符號 ES 應重置為 0");
        }

        [Then(@"玩家點數錢包免扣除下注成本")]
        [Then(@"玩家點數錢包餘額應該保持不變 \(免扣款\)")]
        [Then(@"玩家點數錢包餘額應該保持不變")]
        public void ThenWalletShouldBeFreeFromCost()
        {
            _spinResult.Should().NotBeNull();
            var spinGain = (_spinResult!.Wins ?? new List<WinItem>()).Sum(w => w.Pay * (w.Mp > 0 ? w.Mp : 1.0));
            _spinResult.Wallet.Should().BeApproximately(_walletBeforeSpin + spinGain, 0.001,
                "FreeMode 免費旋轉期間玩家錢包應完全不扣除下注成本 (Wallet = WalletBefore + SpinGain)");
        }

        [Then(@"累積總贏分應該為 (.*)")]
        public void ThenAccumulatedGainShouldBe(double expectedGain)
        {
            _spinResult?.Game.Should().NotBeNull();
            _spinResult!.Game!.Gain.Should().BeApproximately(expectedGain, 0.00001,
                $"FreeMode 累計贏分 Gain 應為 {expectedGain}");
        }

        [When(@"透過作弊產生包含當前 ES 符號的盤面並 Spin")]
        public async Task WhenSpinWithCurrentExpandingSymbol()
        {
            _spinResult?.Game.Should().NotBeNull();
            int es = _spinResult!.Game!.Es;
            es.Should().BeInRange(1, 9, "當前遊戲狀態應已有特殊擴展符號 ES");

            int[][] reelsBon = new int[][]
            {
                new int[] {8, 2, 6, 8, 1, 6, 5, 3, 9, 6, 8, 5, 2, 7, 9, 10, 5, 8, 2, 7, 9, 10, 6, 8, 2, 7, 6, 5, 4, 7, 8, 3, 6, 5, 4, 7, 5},
                new int[] {7, 3, 6, 9, 4, 8, 10, 6, 7, 5, 9, 7, 1, 8, 5, 4, 8, 7, 10, 5, 9, 6, 3, 5, 8, 7, 9, 2, 8, 5, 7, 8, 4, 9, 5, 6, 9},
                new int[] {5, 1, 7, 9, 1, 6, 8, 7, 3, 8, 6, 4, 7, 5, 1, 9, 5, 6, 2, 7, 5, 8, 6, 9, 8, 5, 10, 9, 6, 8, 2, 9, 7, 3, 6, 8},
                new int[] {10, 9, 6, 7, 4, 6, 2, 7, 8, 1, 5, 7, 3, 6, 7, 4, 9, 6, 3, 7, 9, 4, 5, 8, 9, 1, 8, 5, 10, 6, 9, 3, 6, 9, 4, 7, 8},
                new int[] {7, 4, 9, 10, 6, 2, 7, 5, 3, 9, 8, 10, 6, 8, 2, 5, 3, 8, 5, 1, 8, 9, 3, 5, 1, 6, 4, 9, 2, 7, 3, 6, 4, 7, 2, 9, 1},
            };

            int[] cheat = new int[5];
            for (int x = 0; x < 3; x++)
            {
                var reel = reelsBon[x];
                int targetPos = -1;
                for (int pos = 0; pos < reel.Length; pos++)
                {
                    if (reel[(pos + 1) % reel.Length] == es)
                    {
                        targetPos = pos;
                        break;
                    }
                }
                targetPos.Should().NotBe(-1, $"在輪帶 {x} 中應能找到符號 {es}");
                cheat[x] = targetPos;
            }

            for (int x = 3; x < 5; x++)
            {
                var reel = reelsBon[x];
                int targetPos = -1;
                for (int pos = 0; pos < reel.Length; pos++)
                {
                    int s0 = reel[pos];
                    int s1 = reel[(pos + 1) % reel.Length];
                    int s2 = reel[(pos + 2) % reel.Length];
                    if (s0 != es && s1 != es && s2 != es && s0 != 10 && s1 != 10 && s2 != 10)
                    {
                        targetPos = pos;
                        break;
                    }
                }
                targetPos.Should().NotBe(-1, $"在輪帶 {x} 中應能找到避開 {es} 與 Scatter 的位置");
                cheat[x] = targetPos;
            }

            string cheatStr = string.Join(",", cheat);
            await WhenSpinWithCheat(cheatStr);
        }

        [Then(@"應該包含特殊擴展符號 ES 全線 (.*) 條線擴展派彩")]
        public void ThenShouldContainExpandingSymbolFullLineWins(int lineCount)
        {
            _spinResult.Should().NotBeNull();
            _spinResult!.Game.Should().NotBeNull();
            int es = _spinResult.Game!.Es;
            es.Should().BeInRange(1, 9, "特殊擴展符號 ES 應在有效範圍內");

            var expandingWins = _spinResult.Wins.Where(w => w.Sym == es).ToList();
            expandingWins.Count.Should().BeGreaterThanOrEqualTo(lineCount,
                $"應該在全部 {lineCount} 條押註線上皆有特殊擴展符號 ES (符號 {es}) 的派彩");

            var coveredLines = expandingWins.Select(w => w.Li).Distinct().ToList();
            coveredLines.Should().HaveCountGreaterThanOrEqualTo(lineCount,
                $"擴展派彩應覆蓋 {lineCount} 條線");
        }
    }
}
