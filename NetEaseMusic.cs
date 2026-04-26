using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace _163music
{
    public class MusicItem
    {
        public string id = "";
        public string title = "";
        public string title_alias = "";
        public string artist = "";
        public string picture = "";
        public string album = "";
        public string album_alias = "";
        public string cover = "";
        public string company = "";
    }

    public class MusicList
    {
        private List<MusicItem> musicItems;
        private int total = 0;
        private int offset = 0;

        public MusicItem[] Items
        {
            get { return musicItems.ToArray(); }
        }

        public int Offset
        {
            get { return offset; }
        }

        public int Total
        {
            get { return total; }
        }

        public MusicList()
        {
            musicItems = new List<MusicItem>();
        }

        public async void Query(string term)
        {
            NetEaseMusic nease = new NetEaseMusic();
            foreach (MusicItem music in await nease.getMusicByTitle(term))
            {
                musicItems.Add(music);
            }
            offset = nease.ResultOffset;
            total = nease.ResultTotal;
        }
    }

    public class Artist
    {
        public string URL;
        public int ID;
        public string Name;
        public string AltName;
    }

    public class Song
    {
        public int ID;
        public int Track;
        public string? Title;
        public string? Alias;
        public string? Duration;
        public List<Artist> Artists = [];
        public Album? Album;
        public string? URL;
        internal string[]? Details;
    }

    public class Album
    {
        public int ID;
        public string? Title;
        public string? Subtitle;
        public string? Intro;
        public string? Artist;
        public string? PubDate;
        public string? Publisher;
        public string? Cover;
        public List<Song> Songs = [];
        public int Count
        {
            get
            {
                if(Songs == null) new List<Song>();
                return ( Songs.Count );
            }
        }
        public string? URL;

        public async Task<byte[]?> DownloadCover(double size = 600)
        {
            byte[]? data = null;
            try
            {
                if (!string.IsNullOrEmpty(Cover))
                {
                    var bytes = await NetEaseMusic.DownloadImage(Cover);
                    if (bytes is not null)
                    {
                        using var msi = new MemoryStream(bytes);
                        using var mso = new MemoryStream();
                        var img = new BitmapImage() { StreamSource = msi };
                        img.BeginInit();
                        img.StreamSource = msi;
                        img.EndInit();
                        dynamic bmp = size <= 0 ? img : new TransformedBitmap(img, new ScaleTransform(size / img.PixelWidth, size / img.PixelHeight));
                        BitmapEncoder encoder = new JpegBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bmp));
                        encoder.Save(mso);
                        data = mso.ToArray();
                        bytes = null;
                    }
                }
            }
            catch
            {
            }
            return (data);
        }
    }

    public class PlayList
    {
        public string URL;
        public string Title;
        public string Subtitle;
        public string Intro;
        public string CreatedDate;
        public string Creator;
        public List<Song> Songs = new List<Song>();
        public int Count
        {
            get
            {
                if ( Songs == null ) new List<Song>();
                return ( Songs.Count );
            }
        }
    }

    internal class NetEaseMusic
    {
        //反序列化JSON数据  
        char[] charsToTrim = { '*', ' ', '\'', '\"', '\r', '\n' };

        private int queryCount = 0;
        private int queryTotal = 0;
        private int queryOffset = 0;

        public int ResultOffset
        {
            get { return queryOffset; }
        }

        public int ResultCount
        {
            get { return queryCount; }
        }

        public int ResultTotal
        {
            get { return queryTotal; }
        }

        public static async Task<byte[]?> DownloadImage(string url)
        {
            byte[]? data = null;
            try
            {
                using var http = new HttpClient();
                var response = await http.GetAsync(url);
                data = await response.Content.ReadAsByteArrayAsync();
            }
            catch
            {
                
            }
            return (data);
        }

        // Common strip routine
        private string strip(string text, bool keepCRLF = false)
        {
            string result = text.Trim( charsToTrim );
            result = result.Replace("\\r\\n", Environment.NewLine).Replace("\r\n", Environment.NewLine);
            result = result.Replace("\\n\\r", Environment.NewLine).Replace("\n\r", Environment.NewLine);
            result = result.Replace("\\n ", Environment.NewLine).Replace("\n ", Environment.NewLine);
            result = result.Replace("\\r ", Environment.NewLine).Replace("\r ", Environment.NewLine);
            result = result.Replace("\\n", Environment.NewLine).Replace("\n", Environment.NewLine);
            result = result.Replace("\\r", Environment.NewLine).Replace("\r", Environment.NewLine);
            result = result.Replace("\\r\\n\\r\\n", Environment.NewLine).Replace("\r\n\r\n", Environment.NewLine);
            result = result.Replace(Environment.NewLine, "\r");
            if (!keepCRLF)
            {
                result = result.Replace("\\n", "").Replace("\n", "");
                result = result.Replace("\\r", "").Replace("\r", "");
            }
            return (result);
        }

        // Get Album info
        public async Task<Album> getAlbumDetail(int iID)
        {
            List<string> sDetail = new List<string>();
            // http://music.163.com/api/album/ + album_id
            Album album = new Album();

            try
            {
                var uri = new Uri($"http://music.163.com/api/album/{iID}");
                using var http = new HttpClient();
                var response = await http.GetAsync(uri);
                string sContent = await response.Content.ReadAsStringAsync();

                if (sContent.Substring(0, 4).Equals("ERR!"))
                {
                    sDetail.Add("Get album info failed! \r\n EER: \r\n" + sContent.Substring(4));
                    return (album);
                }

                JObject? o = JsonConvert.DeserializeObject(sContent ?? string.Empty) as JObject;
                if (o is not null)
                {
                    if (o["code"].ToString() == "200")
                    {
                        album.URL = $"http://music.163.com/album?id={iID}";
                        album.Title = o["album"]["name"].ToString();
                        //album.Intro = "";
                        //album.PubDate = o["publishTime"].ToString()
                        foreach (var song in o["album"]["songs"])
                        {
                            List<string> artist = new List<string>();
                            List<Artist> artists = new List<Artist>();
                            foreach (var a in song["artists"])
                            {
                                artist.Add(a["name"].ToString());
                                artists.Add(new Artist()
                                {
                                    Name = a["name"].ToString(),
                                    ID = Convert.ToInt32(a["id"].ToString())
                                });
                            }
                            album.Songs.Add(new Song()
                            {
                                URL = $"http://http://music.163.com/song?id={song["id"]}",
                                ID = Convert.ToInt32(song["id"].ToString()),
                                Track = Convert.ToInt32(song["no"].ToString()),
                                Title = song["name"].ToString(),
                                //Alias = "", //string.Join( " / ", song["alias"] );
                                Alias = song["alias"] == null ? "" : string.Join(" / ", song["alias"]),
                                Artists = artists,
                                Album = new Album()
                                {
                                    ID = Convert.ToInt32(song["album"]["id"].ToString()),
                                    Title = song["album"]["name"].ToString(),
                                },
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sDetail.Add("Get album info failed! \r\n EER: \r\n" + ex.Message);
            }
            return (album);
        }

        // Get Album info
        public PlayList getPlayListDetail(int iID)
        {
            // 'http://music.163.com/api/playlist/detail?id=' + '&offset=0&total=true&limit=1001'
            PlayList playlist = new PlayList();



            return (playlist);
        }

        // Get Song Detail infomation
        public async Task<Song?> GetSongDetail(int iID)
        {
            Song? song = null;

            List<string> sDetail = new List<string>();
            try
            {
                string sTitle = "", sAlbum = "", sTrack = "", sCover = "";
                List<string> sAlias = new List<string>();
                List<string> sArtist = new List<string>();

                var uri = new Uri($"http://music.163.com/api/song/detail/?id={iID}&ids=[{iID}]");
                using var http = new HttpClient();
                var response = await http.GetAsync(uri);
                string sContent = await response.Content.ReadAsStringAsync();

                if (sContent.Substring(0, 4).Equals("ERR!"))
                {
                    sDetail.Add("Get title failed! \r\n EER: \r\n" + sContent.Substring(4));
                    return (new Song(){ Details = sDetail.ToArray() });
                }

                JObject o = (JObject)JsonConvert.DeserializeObject(sContent);
                if (o["songs"].HasValues)
                {
                    sTitle = strip(o["songs"][0]["name"].ToString());

                    foreach (string alias in o["songs"][0]["alias"])
                    {
                        sAlias.Add(strip(alias.ToString()));
                    }

                    foreach (JObject artist in o["songs"][0]["artists"])
                    {
                        sArtist.Add(strip(artist["name"].ToString()));
                    }

                    sAlbum = strip(o["songs"][0]["album"]["name"].ToString());

                    int iTrack = Convert.ToInt32(o["songs"][0]["no"]);
                    int iTracks = Convert.ToInt32(o["songs"][0]["album"]["size"]);

                    if (iTracks >= 10000) sTrack = $"{iTrack:D05}";
                    else if (iTracks >= 1000) sTrack = $"{iTrack:D04}";
                    else if (iTracks >= 100) sTrack = $"{iTrack:D03}";
                    else sTrack = $"{iTrack:D02}";

                    sCover = strip(o["songs"][0]["album"]["picUrl"].ToString());

                    sDetail.Add(sTitle);
                    sDetail.Add(string.Join(" ; ", sAlias.ToArray()));
                    sDetail.Add(string.Join(" ; ", sArtist.ToArray()));
                    sDetail.Add(sAlbum);
                    sDetail.Add(sTrack);

                    song = new Song()
                    {
                        ID = iID,
                        Title = sTitle,
                        Alias = sAlias.Any() ? string.Join(" ; ", sAlias) : "",
                        Artists = sArtist.Select(a => new Artist() { Name = a }).ToList(),
                        Album = new Album() { Title = sAlbum, Cover = sCover },
                        Track = iTrack,
                        URL = $"https://music.163.com/song?id={iID}",
                        Details = sDetail.ToArray()
                    };
                }
            }
            catch (Exception ex)
            {
                sDetail.Add("Get title failed! \r\n EER: \r\n" + ex.Message);
                song = new Song() { Details = sDetail.ToArray() };
            }
            return (song);
        }

        // Get Song Lyric for multi-langiages 
        public async Task<string[]> getSongLyric(int iID, bool CRLF = true)
        {

            List<string> sLRC = new List<string>();
            try
            {
                var uri = new Uri("http://music.163.com/api/song/media?id=" + iID);
                using var http = new HttpClient();
                var response = await http.GetAsync(uri);
                string sContent = await response.Content.ReadAsStringAsync();

                if (sContent.Substring(0, 4).Equals("ERR!"))
                {
                    sLRC.Add("Get lyric failed! \r\n EER: \r\n" + sContent.Substring(4));
                    return sLRC.ToArray();
                }

                JObject o = (JObject)JsonConvert.DeserializeObject(sContent);
                sLRC.Add(strip(o["lyric"].ToString(), CRLF));
            }
            catch (Exception ex)
            {
                sLRC.Add("Get lyric failed! \r\n EER: \r\n" + ex.Message);
            }
            return sLRC.ToArray();
        }

        //Get Song Lyric with translated
        public async Task<string[]> getSongLyricMultiLang(int iID)
        {
            List<string> sLRC = new List<string>();
            try
            {
                var uri = new Uri("http://music.163.com/api/song/lyric?os=pc&lv=-1&kv=-1&tv=-1&id=" + iID);
                using var http = new HttpClient();
                var response = await http.GetAsync(uri);
                string sContent = await response.Content.ReadAsStringAsync();

                if (sContent.Substring(0, 4).Equals("ERR!"))
                {
                    sLRC.Add("Get lyric failed! \r\n EER: \r\n" + sContent.Substring(4));
                    return sLRC.ToArray();
                }

                JObject o = (JObject)JsonConvert.DeserializeObject(sContent);

                if ((o.Property("uncollected") != null && (bool)o["uncollected"]) ||
                     (o.Property("nolyric") != null && (bool)o["nolyric"]))
                {
                    sLRC.Add("No Lyric Found!");
                    return (sLRC.ToArray());
                }

                // Original Language Lyric
                if (o["lrc"]["lyric"] != null)
                {
                    string lyric = strip( o["lrc"]["lyric"].ToString(), true );
                    if (lyric.Length > 0)
                    {
                        sLRC.Add(lyric);
                    }
                }
                // Translated Lyric
                if (o["tlyric"]["lyric"] != null)
                {
                    string tlyric = strip( o["tlyric"]["lyric"].ToString(), true );
                    if (tlyric.Length > 0)
                    {
                        sLRC.Add(tlyric);
                    }
                }
                // KaraOk Lyric ?
                if (o["klyric"]["lyric"] != null)
                {
                    string klyric = strip( o["klyric"]["lyric"].ToString(), true );
                    if (klyric.Length > 0)
                    {
                        //sLRC.Add( klyric );
                    }
                }

                if (sLRC.Count <= 0)
                {
                    sLRC.Add("No Lyric Found!");
                }
            }
            catch (Exception ex)
            {
                sLRC.Add("Get lyric failed! \r\n EER: \r\n" + ex.Message);
            }
            return sLRC.ToArray();
        }

        // search music by title
        public async Task<MusicItem[]> getMusicByTitle(string query, int offset = 0, int limit = 100, int type = 1)
        {
            List<MusicItem> sMusic = new List<MusicItem>();
            try
            {
                List<KeyValuePair<string, string>> postParams = new List<KeyValuePair<string, string>>();
                postParams.Add(new KeyValuePair<string, string>("offset", $"{offset}"));
                postParams.Add(new KeyValuePair<string, string>("limit", $"{limit}"));
                ///postParams.Add( new KeyValuePair<string, string>( "type", $"{type}" ) );
                postParams.Add(new KeyValuePair<string, string>("type", "1"));
                postParams.Add(new KeyValuePair<string, string>("s", Uri.EscapeDataString(query).Replace("%20", "+")));

                var uri = new Uri("http://music.163.com/api/search/pc");
                using var http = new HttpClient();
                var content = new FormUrlEncodedContent(postParams);
                var response = await http.PostAsync(uri, content);
                string sContent = await response.Content.ReadAsStringAsync();

                if (sContent.Substring(0, 4).Equals("ERR!"))
                {
                    //sMusic.Add( "Search Music failed! \r\n EER: \r\n" + sContent.Substring( 4 ) );
                    return sMusic.ToArray();
                }

                JObject o = (JObject)JsonConvert.DeserializeObject(sContent);
                if ((int)o["code"] == 200)
                {
                    queryTotal = (int)o["result"]["songCount"];
                    queryOffset = offset;
                    queryCount = 0;

                    if (o["result"]["songs"] != null)
                    {
                        foreach (JObject m in o["result"]["songs"])
                        {
                            MusicItem mItem = new MusicItem();

                            mItem.title = m["name"].ToString();
                            if (m["alias"] != null)
                            {
                                List<string> aliasList = new List<string>();
                                foreach (JValue alias in m["alias"])
                                {
                                    aliasList.Add(alias.ToString());
                                }
                                mItem.title_alias = string.Join(" ; ", aliasList.ToArray());
                            }
                            mItem.id = m["id"].ToString();
                            List<string> arts = new List<string>();
                            List<string> photos = new List<string>();
                            foreach (JObject art in m["artists"])
                            {
                                arts.Add(art["name"].ToString());
                                photos.Add(art["picUrl"].ToString());
                            }
                            mItem.artist = string.Join(" ; ", arts.ToArray());
                            mItem.picture = string.Join(" ; ", photos.ToArray());
                            //mItem.picture = m["album"]["artist"]["picUrl"].ToString();
                            mItem.album = m["album"]["name"].ToString();
                            if (m["album"]["alias"] != null)
                            {
                                List<string> aliasList = new List<string>();
                                foreach (JValue alias in m["album"]["alias"])
                                {
                                    aliasList.Add(alias.ToString());
                                }
                                mItem.album_alias = string.Join(" ; ", aliasList.ToArray());
                            }
                            mItem.cover = m["album"]["picUrl"].ToString();
                            mItem.company = m["album"]["company"].ToString();

                            sMusic.Add(mItem);

                        }
                    }
                }
                queryCount += sMusic.Count;
                o.RemoveAll();
            }
            catch (Exception ex)
            {
                //sMusic.Add( "Search Music failed! \r\n EER: \r\n" + ex.Message );
            }
            return sMusic.ToArray();
        }
    }
}
