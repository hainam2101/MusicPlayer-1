﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio;
using NAudio.Wave;
using NAudio.MediaFoundation;
using MusicPlayer.Extensions;
using MusicPlayer.Models;
using TagLib;
using System.Net;
using MusicPlayer.UI;

namespace MusicPlayer.Controller
{
    /// <summary>
    /// Plays the music
    /// </summary>
    internal partial class Player
    {
        #region Variables

        /// <summary>
        /// Conatins the list of songs (absolute paths)
        /// </summary>
        private List<Song> _sourceList;
        private Random random;

        /// <summary>
        ///  audio output.
        /// </summary>
        private WaveOut waveOutDevice;

        /// <summary>
        /// The server instance when hosting the music.
        /// </summary>
        private NetworkServer _networkServer;

        /// <summary>
        /// Hosting a music stream over network
        /// </summary>
        private bool hosting = false;

        /// <summary>
        /// Indicates whether the class is owned by another class and is only receiving commands.
        /// </summary>
        private bool _isReceiveMode;

        /// <summary>
        /// Indicates that the class is disposing, all threads should have this var in the while loop.
        /// </summary>
        private bool _disposing = false;

        private bool locker;

        /// <summary>
        /// The current song.
        /// </summary>
        private Song currentSong;

        public Song ActiveSong
        {
            get
            {
                return this.currentSong;
            }
        }

        private Thread timetracker;
        private IUI gui;

        private SongController songCtrl;
        private MediaFoundationReader playstream;

        #endregion

        /// <summary>
        /// Initialises the player
        /// </summary>
        /// <param name="gui">The GUI</param>
        public Player(IUI gui, bool isReceiveMode = false) 
        {
            this.gui = gui;
            waveOutDevice = new WaveOut();
            _sourceList = new List<Song>();
            waveOutDevice.PlaybackStopped += new EventHandler<StoppedEventArgs>(OnWaveOutStop);
            locker = false;
            this.songCtrl = new SongController();
            random = new Random();
            this._isReceiveMode = isReceiveMode;
        }

        #region PlayerInformation

        /// <summary>
        /// Returns a boolean whether the player is playing.
        /// </summary>
        /// <returns>The boolean.</returns>
        public bool IsPlaying()
        {
            if (waveOutDevice != null && (waveOutDevice.PlaybackState == PlaybackState.Playing) && playstream != null)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the current song collection.
        /// </summary>
        public List<Song> SongList
        {
            get
            {
                return _sourceList;
            }
        }

        /// <summary>
        /// Gets the current time.
        /// </summary>
        /// <returns>The current time or null.</returns>
        public TimeSpan? GetCurrentTime()
        {
            if (IsPlaying() && playstream != null)
            {
                return playstream.CurrentTime;
            }

            return null;
        }

        #endregion

        #region PlayerControls

        /// <summary>
        /// Pauses or plays the music
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void PausePlay(object sender, EventArgs e) 
        {
            try
            {
                if (waveOutDevice.PlaybackState == PlaybackState.Playing)
                {
                    waveOutDevice.Pause();
                }
                else
                {
                    waveOutDevice.Play();
                    gui.SetSongDuration(playstream.TotalTime);
                    ////gui.redrawTrackbar(0, (int) playstream.TotalTime.TotalSeconds);
                    if (timetracker != null)
                    {
                        timetracker.Abort();
                    }

                    timetracker = new Thread(UpdateTime);
                    timetracker.Start();
                }
            }
            catch (Exception ee)
            {
                if (!_isReceiveMode)
                {
                    MessageBox.Show("ERROR on PLAY: " + ee.Message + "\n" + ee.InnerException);
                }
            }
        }

        /// <summary>
        /// Loads a song
        /// </summary>
        /// <param name="song">The song object.</param>
        public void Load(Song song)
        {
            currentSong = song;
            this.Load(song.Location);
        }

        /// <summary>
        /// Load a song
        /// </summary>
        /// <param name="path">the absolute path</param>
        private void Load(string path)
        {
            // dispose old mp3
            if (waveOutDevice.PlaybackState == PlaybackState.Playing || waveOutDevice.PlaybackState == PlaybackState.Paused)
            {
                locker = true;
                try
                {
                    waveOutDevice.Stop();
                }
                catch
                {
                }

                waveOutDevice.Dispose();
                locker = false;
                waveOutDevice = new WaveOut();
                waveOutDevice.PlaybackStopped += new EventHandler<StoppedEventArgs>(OnWaveOutStop);
            }

            string extension = Path.GetExtension(path);

            // Start foundation reader
            try
            {
                if (playstream != null)
                {
                    playstream.Dispose();
                    playstream = null;
                }

                playstream = new MediaFoundationReader(path, new MediaFoundationReader.MediaFoundationReaderSettings
                {
                    RepositionInRead = true,
                    SingleReaderObject = true
                });
                //playstream = new MediaFoundationReader(path);

                if (playstream != null)
                {
                    waveOutDevice.Init(playstream);
                    if (_networkServer != null)
                    {
                        _networkServer.HostSong(currentSong);
                    }

                    // Todo: ensure this exists
                    gui.SetSong(currentSong);
                    ////gui.SetActive(this);
                }
                else
                {
                    if (!_isReceiveMode)
                    {
                        MessageBox.Show("File was not found or could not be opened at:" + path);
                    }
                }
            }
            catch (SystemException ee) 
            {
                if (!_isReceiveMode)
                {
                    NextRandomSong();
                }
            }
            catch (Exception e) 
            {
                Console.WriteLine("ERRROR on Load: " + e.Message + "\n" + e.StackTrace + "\n" + e.InnerException);
                if (!_isReceiveMode)
                {
                    MessageBox.Show("Your system dus not support this audio codec: " + extension +
                            "\n Please install the media foundation codec");
                }
            }

            PausePlay(null, null);
        }

        /// <summary>
        /// Loads a list of file locations.
        /// </summary>
        /// <param name="files">The file locations.</param>
        /// <param name="number">The number of files to load.</param>
        /// <returns>A List of songs.</returns>
        public List<Song> LoadAll(string[] files, int? number){
            if (waveOutDevice != null && waveOutDevice.PlaybackState != PlaybackState.Stopped)
            {
                locker = true;
                waveOutDevice.Stop();
            }

            List<Song> filestoload = new List<Song>();

            if (number != null)
            {
                Random r = new Random();
                while (filestoload.Count < number) 
                {
                    filestoload.Add(new Song(TakeRandom(files)));
                }
            }

            if (filestoload.Count == 0)
            {
                _sourceList = new List<Song>();
                foreach (string st in files)
                {
                    string ex = Path.GetExtension(st).ToLower();
                    if ((ex == ".mp3" || ex == ".flac" || ex == ".wma") && st.IndexOfAny(System.IO.Path.GetInvalidPathChars()) < 0)
                    {
                        Song newSong = new Song(st);
                        _sourceList.Add(newSong);
                    }
                }
            }
            else 
            {
                _sourceList = filestoload;
            }

            //NextRandomSong();
            EnrichSource(null);
            
            return _sourceList;
        }
         
        /// <summary>
        /// Play a song object.
        /// </summary>
        /// <param name="song">The song.</param>
        public void Play(Song song)
        {
            if (!_sourceList.Any(ct => ct.Location == song.Location))
            {
                _sourceList.Add(song);
                ////gui.SetSongs(_sourceList);
                ////gui.AddSongsToListView(sourceList);
            }

            this.Play(song.Location);
        }

        /// <summary>
        /// Play from key (path)
        /// </summary>
        /// <param name="key"></param>
        public void Play(string key) {

            var song = _sourceList.FirstOrDefault(ct => ct.Location == key);
            if (song != null)
            {
                Load(song);
            }
            else 
            {
                MessageBox.Show("Error: " + key + " not found!");
            }
        }

        /// <summary>
        /// Starts a next random song from the source list.
        /// </summary>
        public void NextRandomSong()
        {
            if (!locker)
            {
                if (_sourceList != null && _sourceList.Count > 0)
                {
                    var song = _sourceList[random.Next(0, _sourceList.Count)];
                    if (!hosting || Path.GetExtension(song.Location) == ".mp3")
                    {
                        Play(song);
                    }
                    else
                    {
                        NextRandomSong();
                    }
                }
            }
            else
            {
                locker = false;
            }
        }

        /// <summary>
        /// Copies all songs from the sourcelist to the destination folder
        /// </summary>
        /// <param name="destination"></param>
        public void CopyRandomSongs(string destination)
        {
            List<Song> copylist = this._sourceList.ToList();
            new Thread(() => new Progress(copylist, destination).ShowDialog()).Start();
        }

        /// <summary>
        /// Sets the Song position to a certain time.
        /// </summary>
        /// <param name="time">The time.</param>
        public void MoveToTime(TimeSpan time)
        {
            playstream.CurrentTime = time;
            if (hosting && _networkServer != null)
            {
                _networkServer.GotoPosition(time);
            }
        }

        #endregion

        /// <summary>
        /// Updates the time in the GUI.
        /// </summary>
        private void UpdateTime()
        {
            try
            {
                while (!_disposing && waveOutDevice != null 
                    && (waveOutDevice.PlaybackState == PlaybackState.Paused || waveOutDevice.PlaybackState == PlaybackState.Playing))
                {
                    gui.SetSongPosition(playstream.CurrentTime);
                    ////gui.SetTrackbarPos((int)playstream.CurrentTime.TotalSeconds);
                    ThreadExtensions.SaveSleep(500);
                }
            }
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// Next random song from the sourceList
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnWaveOutStop(object sender, EventArgs e) {
            if (!_isReceiveMode)
            {
                NextRandomSong();
            }
            else
            {
                if (playstream != null && !locker)
                {
                    locker = true;
                    var pos = playstream.Position;
                    if (pos < playstream.Length)
                    {
                        var task = Task.Run(delegate()
                        {
                            //waveOutDevice.Dispose();
                            //waveOutDevice = new WaveOut();
                            //waveOutDevice.Init(playstream);
                            playstream.Seek(pos, SeekOrigin.Begin);
                            waveOutDevice.Play();
                        });

                        if (task.Wait(2000))
                        {
                            waveOutDevice.Play();
                        }
                    }

                    locker = false;
                }
            }
        }

        /// <summary>
        /// Trackbar scroll event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void MoveSongPosition(object sender, EventArgs e)
        {
            try
            {
                TrackBar temp = (TrackBar)sender;
                int pos = temp.Value;
                MoveToTime(new TimeSpan(0, 0, pos));
            }
            catch { }
        }

        /// <summary>
        /// Starts the audio server. 
        /// </summary>
        /// <param name="ip">Thge ip adress string.</param>
        /// <param name="port">The port string.</param>
        public void StartAudioServer(IPAddress ip, int port)
        {
            hosting = true;
            _networkServer = new NetworkServer(port, this);
            _networkServer.HostSong(currentSong);
        }

        /// <summary>
        /// Notifies the clients when the server is active.
        /// </summary>
        /// <param name="text">The text.</param>
        public void NotifyClients(string text)
        {
            if(_networkServer != null)
            {
                _networkServer.Notify(text);
            }
        }

        /// <summary>
        /// Disconnects from the audio server
        /// </summary>
        public void DisconnectFromAudioServer()
        {
            if (waveOutDevice.PlaybackState == PlaybackState.Playing || waveOutDevice.PlaybackState == PlaybackState.Paused)
            {
                locker = true;
                waveOutDevice.Stop();
                locker = false;
            }

            if (_networkServer != null)
            {
                _networkServer.Dispose();
                _networkServer = null;
            }
        }

        /// <summary>
        /// Takes a random valid song
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        private string TakeRandom(string[] list)
        {
            string rand = list[random.Next(0, list.Length - 1)];
            string ex = Path.GetExtension(rand).ToLower();
            if (!(ex == ".mp3" || ex == ".flac" || ex == ".wma"))
            {
                rand = TakeRandom(list);
            }

            return rand;
        }

        /// <summary>
        /// Copies a mediafoundation reader stream to a buffered waveprovider.
        /// Sets the pos of the mediafoundstream to 0 when done.
        /// </summary>
        /// <param name="input">The source stream.</param>
        /// <returns>The buffered wave provider.</returns>
        private BufferedWaveProvider CopyStream(MediaFoundationReader input)
        {
            BufferedWaveProvider test = new BufferedWaveProvider(WaveFormat.CreateCustomFormat(WaveFormatEncoding.Pcm, 44100, 2, 176400, 4, 16));
            test.BufferDuration = input.TotalTime + new TimeSpan(0, 0, 1);
            test.DiscardOnBufferOverflow = true;
            using (WaveStream waveStream = WaveFormatConversionStream.CreatePcmStream(input))
            {
                long previouspos = 0;
                while ((waveStream.Length - waveStream.Position) > 0)
                {
                    int size = 8092;
                    if (size > waveStream.Length - waveStream.Position)
                    {
                        size = (int)(waveStream.Length - waveStream.Position);
                    }

                    byte[] bytes = new byte[size];
                    waveStream.Read(bytes, 0, size);
                    test.AddSamples(bytes, 0, size);
                    previouspos += size;

                    if (size != 8092)
                    {
                        break;
                    }
                }
            }

            input.Position = 0;
            return test;
        }

        /// <summary>
        /// Disposes this class properly
        /// </summary>
        public void Dispose()
        {
            try
            {
                _disposing = true;
                locker = true;
                songCtrl.Dispose();

                if (_networkServer != null)
                {
                    _networkServer.Dispose();
                }

                if (waveOutDevice != null)
                {
                    waveOutDevice.Dispose();
                    waveOutDevice = null;
                }

                if (playstream != null)
                {
                    playstream.Dispose();
                }
            }
            catch 
            {
            }
        }
    }
}
