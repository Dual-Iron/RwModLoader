using Music;
using System;

namespace Realm.Gui
{
    public sealed class ModsMenuMusic
    {
        private enum Mode : byte
        {
            Unstarted, Playing, Stopped
        }

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

        private static string Song => DateTime.Now.Hour switch {
            < 4 => "NA_39 - Cracked Earth",
            < 8 => "NA_04 - Silicon",
            < 12 => "NA_30 - Distance",
            < 16 => "NA_24 - Emotion Thread",
            < 20 => "NA_09 - Interest Pad",
            _ => "RW_16 - Shoreline",
        };

        public ModsMenuMusic(ProcessManager manager)
        {
            this.manager = manager;
        }

        private MusicPlayer? Music => manager.musicPlayer;

        private readonly ProcessManager manager;

        private Mode mode;

        private void SimpleRequestSong(MusicPlayer mp, string name, float fadeInTime)
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

        public void Start()
        {
            if (Music != null && mode == Mode.Unstarted) {
                Music.FadeOutAllSongs(40);
                SimpleRequestSong(Music, Song, 10);
                startIntroMusic = false;
                mode = Mode.Playing;
            }
        }

        public void ShutDown()
        {
            if (Music != null && mode == Mode.Playing) {
                Music.FadeOutAllSongs(40);
                Music.RequestIntroRollMusic();
                startIntroMusic = true;
                mode = Mode.Stopped;
            }
        }
    }
}