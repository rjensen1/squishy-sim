using SquishySim.Body;

namespace SquishySim.Tests.Body;

public class DriveSystemSocialTests
{
    // ── AC1: Social decay without interaction ────────────────────────────────

    [Fact]
    public void WhenNoQualifyingInteraction_SocialIncreasesBy_SocialDecayRate()
    {
        const float SocialDecayRate = 0.012f;
        var state = new BodyState { Social = 0.20f };
        var before = state.Social;

        DriveSystem.Tick(state, hadQualifyingInteraction: false);

        Assert.Equal(SocialDecayRate, state.Social - before, precision: 6);
    }

    [Fact]
    public void WhenNoQualifyingInteraction_SocialDefaultParam_SocialIncreases()
    {
        // Verify the default (no interaction) behavior without passing the param
        const float SocialDecayRate = 0.012f;
        var state = new BodyState { Social = 0.20f };
        var before = state.Social;

        DriveSystem.Tick(state);

        Assert.Equal(SocialDecayRate, state.Social - before, precision: 6);
    }

    // ── AC2: Social satisfaction on qualifying interaction ───────────────────

    [Fact]
    public void WhenHadQualifyingInteraction_SocialDecreasesBySatisfactionAmount()
    {
        const float SatisfactionAmount = 0.25f;
        var state = new BodyState { Social = 0.70f };
        var before = state.Social;

        DriveSystem.Tick(state, hadQualifyingInteraction: true);

        Assert.Equal(SatisfactionAmount, before - state.Social, precision: 6);
    }

    // ── AC3: Mutual exclusivity — decay and satisfaction don't both apply ────

    [Fact]
    public void WhenHadQualifyingInteraction_SocialDecayDoesNotAlsoApply()
    {
        const float SocialDecayRate    = 0.012f;
        const float SatisfactionAmount = 0.25f;
        var state = new BodyState { Social = 0.70f };
        var before = state.Social;

        DriveSystem.Tick(state, hadQualifyingInteraction: true);

        var delta = state.Social - before;
        // Only satisfaction applied (negative), not decay (positive) — net is -(0.25f)
        Assert.Equal(-SatisfactionAmount, delta, precision: 6);
        Assert.NotEqual(-SatisfactionAmount + SocialDecayRate, delta, precision: 6);
    }

    [Fact]
    public void WhenNoQualifyingInteraction_SatisfactionDoesNotAlsoApply()
    {
        const float SocialDecayRate = 0.012f;
        var state = new BodyState { Social = 0.20f };
        var before = state.Social;

        DriveSystem.Tick(state, hadQualifyingInteraction: false);

        // Only decay applied (positive), not satisfaction (negative)
        Assert.Equal(SocialDecayRate, state.Social - before, precision: 6);
    }

    // ── AC4: Social trigger threshold ────────────────────────────────────────

    [Fact]
    public void WhenSocialAboveTrigger_SocialIsAtUrgentOrHigherLabel()
    {
        // Trigger is at Social > 0.65f — verify state labels reflect this
        var state = new BodyState { Social = 0.70f };
        Assert.Equal("urgent", state.SocialLabel);
    }

    [Fact]
    public void WhenSocialBelowTrigger_SocialLabelIsModerateOrLow()
    {
        var state = new BodyState { Social = 0.50f };
        Assert.Equal("moderate", state.SocialLabel);
    }

    // ── AC5: Mood distress from social isolation ─────────────────────────────

    [Fact]
    public void WhenSocialAtOrAboveMoodThreshold_MoodDecreases()
    {
        const float MoodThreshold = 0.70f;
        var state = new BodyState { Social = MoodThreshold, Mood = 0.80f };
        var moodBefore = state.Mood;

        DriveSystem.Tick(state, hadQualifyingInteraction: false);

        Assert.True(state.Mood < moodBefore,
            $"Expected mood to decrease from {moodBefore} but got {state.Mood}");
    }

    [Fact]
    public void WhenSocialBelowMoodThreshold_MoodNotAffectedBySocialDrive()
    {
        // Social = 0.50f, no other drives elevated — mood should be unchanged by social cross-effect
        var state = new BodyState
        {
            Social  = 0.50f,
            Mood    = 0.80f,
            Hunger  = 0.10f,
            Thirst  = 0.10f,
            Fatigue = 0.10f,
            Bladder = 0.10f
        };
        var moodBefore = state.Mood;

        DriveSystem.Tick(state, hadQualifyingInteraction: false);

        Assert.Equal(moodBefore, state.Mood, precision: 6);
    }

    [Fact]
    public void WhenSocialBelowThresholdEvenAfterDecay_MoodIsUnchanged()
    {
        // Social = 0.68f. After decay (+0.012f) = 0.692f — still below 0.70f threshold.
        // No social distress should apply.
        var state = new BodyState
        {
            Social  = 0.68f,
            Mood    = 0.80f,
            Hunger  = 0.10f,
            Thirst  = 0.10f,
            Fatigue = 0.10f,
            Bladder = 0.10f
        };
        var moodBefore = state.Mood;

        DriveSystem.Tick(state, hadQualifyingInteraction: false);

        Assert.Equal(moodBefore, state.Mood, precision: 6);
    }

    [Fact]
    public void WhenSocialAt0_80_MoodDistressMatchesFormula()
    {
        // Within Tick: decay runs first (+0.012f), then distress is computed.
        // Social after decay = 0.80f + 0.012f = 0.812f
        // distress = (0.812f - 0.70f) * 0.12f = 0.01344f
        const float SocialDecayRate   = 0.012f;
        const float MoodThreshold     = 0.70f;
        const float MoodDistressCoeff = 0.12f;
        const float StartingSocial    = 0.80f;
        float expectedDistress = (StartingSocial + SocialDecayRate - MoodThreshold) * MoodDistressCoeff;

        var state = new BodyState
        {
            Social  = StartingSocial,
            Mood    = 0.80f,
            Hunger  = 0.10f,
            Thirst  = 0.10f,
            Fatigue = 0.10f,
            Bladder = 0.10f
        };
        var moodBefore = state.Mood;

        DriveSystem.Tick(state, hadQualifyingInteraction: false);

        Assert.Equal(expectedDistress, moodBefore - state.Mood, precision: 6);
    }

    // ── AC boundary: Social clamps to 0 after large satisfaction ────────────

    [Fact]
    public void WhenSocialLowAndHadInteraction_SocialClampsToZero()
    {
        var state = new BodyState { Social = 0.10f };  // 0.10f - 0.25f satisfaction = -0.15f → clamped to 0

        DriveSystem.Tick(state, hadQualifyingInteraction: true);

        Assert.Equal(0f, state.Social);
    }
}
