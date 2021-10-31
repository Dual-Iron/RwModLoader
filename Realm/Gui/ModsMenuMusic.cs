using Music;

namespace Realm.Gui;

static class ModsMenuMusic
{
    private static bool startIntroMusic;

    public static void Hook()
    {
        On.Music.IntroRollMusic.Update += IntroRollMusic_Update;
    }

    private static void IntroRollMusic_Update(On.Music.IntroRollMusic.orig_Update orig, IntroRollMusic self)
    {
        if (startIntroMusic && self.startedPlaying && !self.FadingOut) {
            self.StartMusic();
            startIntroMusic = false;
        }

        orig(self);
    }

    private static bool playing;

    private static void SimpleRequestSong(MusicPlayer mp, string name, float fadeInTime)
    {
        const float priority = 1000;

        MenuOrSlideShowSong song = new(mp, name, priority, fadeInTime) {
            Loop = true
        };

        if (mp.song == null) {
            mp.song = song;
            mp.song.playWhenReady = true;
        } else {
            mp.nextSong = song;
            mp.nextSong.playWhenReady = false;
        }
    }

    public static void Start(MusicPlayer music)
    {
        if (music != null && !playing) {
            music.FadeOutAllSongs(40);
            SimpleRequestSong(music, GuiHandler.TimedSong, 10);
            startIntroMusic = false;
            playing = true;
        }
    }

    public static void ShutDown(MusicPlayer music)
    {
        if (music != null && playing) {
            music.FadeOutAllSongs(40);
            music.RequestIntroRollMusic();
            startIntroMusic = true;
            playing = false;
        }
    }
}
