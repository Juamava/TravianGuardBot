using MainCore.Parsers;

namespace MainCore.Test
{
    public class EvadeRulesTests
    {
        private static readonly DateTime Now = new(2026, 11, 7, 12, 0, 0);

        private static IncomingAttack At(int seconds) => new(Now.AddSeconds(seconds), "", false);

        [Fact]
        public void Attacks_a_few_seconds_apart_are_one_train()
        {
            var waves = EvadeRules.GroupWaves([At(300), At(305), At(310), At(316)], gapSeconds: 10);

            waves.Count.ShouldBe(1);
            waves[0].First.ShouldBe(Now.AddSeconds(300));
            waves[0].Last.ShouldBe(Now.AddSeconds(316));
            waves[0].Count.ShouldBe(4);
        }

        [Fact]
        public void A_long_pause_starts_a_new_wave()
        {
            var waves = EvadeRules.GroupWaves([At(900), At(300), At(305)], gapSeconds: 60);

            waves.Count.ShouldBe(2);
            waves[0].Count.ShouldBe(2);
            waves[1].First.ShouldBe(Now.AddSeconds(900));
        }

        [Fact]
        public void A_single_attack_is_cancelled_so_troops_come_back_after_it()
        {
            var wave = new AttackWave(Now.AddSeconds(10), Now.AddSeconds(10), 1);
            var sentAt = EvadeRules.SendAt(wave, leadSeconds: 10);

            var cancelAt = EvadeRules.CancelAt(wave, sentAt, safetySeconds: 4, travelSeconds: 600);

            cancelAt.ShouldNotBeNull();
            EvadeRules.HomeAt(sentAt, cancelAt.Value).ShouldBe(Now.AddSeconds(14));
            cancelAt.Value.ShouldBe(Now.AddSeconds(7));
        }

        [Fact]
        public void A_train_longer_than_the_cancel_window_keeps_the_troops_with_the_ally()
        {
            var wave = new AttackWave(Now.AddSeconds(10), Now.AddSeconds(400), 30);
            var sentAt = EvadeRules.SendAt(wave, leadSeconds: 10);

            EvadeRules.CancelAt(wave, sentAt, safetySeconds: 5, travelSeconds: 1200).ShouldBeNull();
        }

        [Fact]
        public void A_close_ally_cannot_be_cancelled_after_the_troops_arrive()
        {
            var wave = new AttackWave(Now.AddSeconds(10), Now.AddSeconds(60), 5);
            var sentAt = EvadeRules.SendAt(wave, leadSeconds: 10);

            EvadeRules.CancelAt(wave, sentAt, safetySeconds: 5, travelSeconds: 20).ShouldBeNull();
        }

        [Fact]
        public void New_attacks_of_the_train_extend_the_wave()
        {
            var wave = new AttackWave(Now.AddSeconds(10), Now.AddSeconds(20), 2);

            var extended = EvadeRules.Extend(wave, [At(20), At(30), At(38), At(200)], gapSeconds: 15);

            extended.Last.ShouldBe(Now.AddSeconds(38));
            extended.Count.ShouldBe(4);
        }

        [Fact]
        public void Troops_with_the_ally_come_back_only_when_the_attacks_stop()
        {
            var waveLast = Now.AddSeconds(30);

            EvadeRules.CanRecall(Now, travelSeconds: 20, waveLast, [], leadSeconds: 10, safetySeconds: 5).ShouldBeFalse();
            EvadeRules.CanRecall(Now, travelSeconds: 60, waveLast, [At(90)], leadSeconds: 10, safetySeconds: 5).ShouldBeFalse();
            EvadeRules.CanRecall(Now, travelSeconds: 60, waveLast, [At(3600)], leadSeconds: 10, safetySeconds: 5).ShouldBeTrue();
        }

        [Theory]
        [InlineData("In 0:04:47 hours", 287)]
        [InlineData("en 1:00:05 h.", 3605)]
        [InlineData("", -1)]
        public void Travel_time_is_read(string text, int expected)
        {
            EvadeRules.ParseDuration(text).ShouldBe(expected);
        }
    }
}
