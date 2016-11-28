﻿using MusicPlayer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;

namespace MusicPlayer.Controller
{
    /// <summary>
    /// The Data part of the player.
    /// </summary>
    internal partial class Player
    {
        /// <summary>
        /// Gets a song from the list at the certain location
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public Song GetSongAtLocation(string location)
        {
            Song result = null;
            if (sourceList != null)
            {
                result = sourceList.FirstOrDefault(ct => ct.Location == location);
            }

            return result;
        }

        /// <summary>
        /// sets a song in the sourcellist if it exist
        /// (Renews the data)
        /// </summary>
        /// <param name="song">The new data</param>
        /// <returns>The Song.</returns>
        public Song SetSong(Song song)
        {
            int index = -1;
            if (sourceList != null)
            {
                index = sourceList.FindIndex(ct => ct.Location == song.Location);
            }

            if (index > -1)
            {
                sourceList[index] = song;
            }
            else
            {
                song = null;
            }

            return song;
        }

        /// <summary>
        /// Loads the deatisl of a song into the model
        /// Adds the details to the db
        /// </summary>
        /// <param name="song">the song to load info of</param>
        /// <returns>the loaded song or null</returns>
        public Song LoadDetails(Song song)
        {
            Song result = null;

            try
            {
                var file = TagLib.File.Create(song.Location);
                song.Gengre = string.Join(", ", file.Tag.Genres);
                song.Album = file.Tag.Album;

                string band = string.Join(", ", file.Tag.AlbumArtists);
                if (!string.IsNullOrEmpty(band))
                {
                    song.Band = band;
                }

                song.DateAdded = new FileInfo(song.Location).CreationTime;
                if (file.Tag.Year > 0)
                {
                    song.DateCreated = new DateTime((int)file.Tag.Year, 1, 1);
                }

                string title = file.Tag.Title;
                if (title != null)
                {
                    title = title.TrimStart();
                    if (title != string.Empty)
                        song.Title = title;
                }

                songCtrl.AddSongToDb(song);
                result = SetSong(song);

                file.Dispose();
            }
            catch (CorruptFileException cor)
            {
                // TODO
            }

            return result;
        }

        /// <summary>
        /// Enriches the data of the sourceList via the db
        /// </summary>
        /// <param name="rootFolder">optional parameter</param>
        private void EnrichSource(string rootFolder)
        {
            if (rootFolder == null)
            {
                rootFolder = GetRootFolder(sourceList.Select(ct => ct.Location).ToArray());
            }

            var dbSongs = songCtrl.GetAllForFolder(rootFolder);
            for (int i = 0; i < sourceList.Count; i++)
            {
                var temp = dbSongs.FirstOrDefault(ct => ct.Location == sourceList[i].Location);
                if (temp != null)
                {
                    sourceList[i] = temp;
                }
            }
        }

        /// <summary>
        /// Gets the rootfolder from a collection of strings
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        private string GetRootFolder(string[] ss)
        {
            if (ss.Length == 0)
            {
                return "";
            }

            if (ss.Length == 1)
            {
                return ss[0];
            }

            int prefixLength = 0;

            foreach (char c in ss[0])
            {
                foreach (string s in ss)
                {
                    if (s.Length <= prefixLength || s[prefixLength] != c)
                    {
                        return ss[0].Substring(0, prefixLength);
                    }
                }
                prefixLength++;
            }

            return ss[0]; // all strings identical
        }
    }
}
