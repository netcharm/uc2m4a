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

#pragma warning disable IDE0130 // 命名空间与文件夹结构不匹配
namespace _163music
{
#pragma warning disable IDE0079
#pragma warning disable IDE0044
#pragma warning disable IDE0059 // 不需要赋值
#pragma warning disable IDE0060
#pragma warning disable IDE0220
#pragma warning disable SYSLIB1045
#pragma warning disable CS8622 // 参数类型中引用类型的为 Null 性与目标委托不匹配(可能是由于为 Null 性特性)。

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
        private List<MusicItem> musicItems = [];
        private int total = 0;
        private int offset = 0;

        public MusicItem[] Items
        {
            get { return [.. musicItems]; }
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
            musicItems = [];
        }

        public async void Query(string term)
        {
            NetEaseMusic nease = new();
            foreach (MusicItem music in await nease.GetMusicByTitle(term))
            {
                musicItems.Add(music);
            }
            offset = nease.ResultOffset;
            total = nease.ResultTotal;
        }
    }

    public class Artist
    {
        public int ID;
        public string? Name;
        public string? AltName;
        public string? URL;
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
                Songs ??= [];
                return (Songs.Count);
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
                        img.CacheOption = BitmapCacheOption.None;
                        img.StreamSource = msi;
                        img.EndInit();
                        BitmapSource bmp = size > 0 && size < img.PixelWidth  && size < img.PixelHeight ? new TransformedBitmap(img, new ScaleTransform(size / img.PixelWidth, size / img.PixelHeight)) : img;
                        BitmapEncoder encoder = new JpegBitmapEncoder() { QualityLevel = 75, Metadata = new BitmapMetadata("jpg") { Title = Title, Subject = Subtitle, Comment = (Intro + Environment.NewLine + URL).Trim() } };
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
        public string? Title;
        public string? Subtitle;
        public string? Intro;
        public string? CreatedDate;
        public string? Creator;
        public List<Song> Songs = [];
        public int Count
        {
            get
            {
                Songs ??= [];
                return (Songs.Count);
            }
        }
        public string? URL;
    }

    internal class NetEaseMusic
    {
        //反序列化JSON数据  
        private readonly char[] charsToTrim = ['*', ' ', '\'', '\"', '\r', '\n'];

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
        private string Strip(string text, bool keepCRLF = false)
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
        public static async Task<Album> GetAlbumDetail(int iID)
        {
            List<string> sDetail = [];
            // http://music.163.com/api/album/ + album_id
            Album album = new();

            try
            {
                var uri = new Uri($"http://music.163.com/api/album/{iID}");
                using var http = new HttpClient();
                var response = await http.GetAsync(uri);
                string sContent = await response.Content.ReadAsStringAsync();

                if (sContent[..4].Equals("ERR!"))
                {
                    sDetail.Add(string.Concat("Get album info failed! \r\n EER: \r\n", sContent.AsSpan(4)));
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
                            List<string> artist = [];
                            List<Artist> artists = [];
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
        public static PlayList GetPlayListDetail(int iID)
        {
            // 'http://music.163.com/api/playlist/detail?id=' + '&offset=0&total=true&limit=1001'
            PlayList playlist = new();



            return (playlist);
        }

        // Get Song Detail infomation
        public async Task<Song?> GetSongDetail(int iID)
        {
            Song? song = null;

            List<string> sDetail = [];
            try
            {
                string sTitle = "", sAlbum = "", sTrack = "", sCover = "";
                List<string> sAlias = [];
                List<string> sArtist = [];

                var uri = new Uri($"http://music.163.com/api/song/detail/?id={iID}&ids=[{iID}]");
                using var http = new HttpClient();
                var response = await http.GetAsync(uri);
                string sContent = await response.Content.ReadAsStringAsync();

                if (sContent[..4].Equals("ERR!"))
                {
                    sDetail.Add(string.Concat("Get title failed! \r\n EER: \r\n", sContent.AsSpan(4)));
                    return (new Song() { Details = [.. sDetail] });
                }

                JObject o = (JObject)JsonConvert.DeserializeObject(sContent);
                if (o["songs"].HasValues)
                {
                    sTitle = Strip(o["songs"][0]["name"].ToString());

                    foreach (string alias in o["songs"][0]["alias"])
                    {
                        sAlias.Add(Strip(alias.ToString()));
                    }

                    foreach (JObject artist in o["songs"][0]["artists"])
                    {
                        sArtist.Add(Strip(artist["name"].ToString()));
                    }

                    sAlbum = Strip(o["songs"][0]["album"]["name"].ToString());

                    int iTrack = Convert.ToInt32(o["songs"][0]["no"]);
                    int iTracks = Convert.ToInt32(o["songs"][0]["album"]["size"]);

                    if (iTracks >= 10000) sTrack = $"{iTrack:D05}";
                    else if (iTracks >= 1000) sTrack = $"{iTrack:D04}";
                    else if (iTracks >= 100) sTrack = $"{iTrack:D03}";
                    else sTrack = $"{iTrack:D02}";

                    sCover = Strip(o["songs"][0]["album"]["picUrl"].ToString());

                    sDetail.Add(sTitle);
                    sDetail.Add(string.Join(" ; ", sAlias.ToArray()));
                    sDetail.Add(string.Join(" ; ", sArtist.ToArray()));
                    sDetail.Add(sAlbum);
                    sDetail.Add(sTrack);

                    song = new Song()
                    {
                        ID = iID,
                        Title = sTitle,
                        Alias = sAlias.Count > 0 ? string.Join(" ; ", sAlias) : "",
                        Artists = [.. sArtist.Select(a => new Artist() { Name = a })],
                        Album = new Album() { Title = sAlbum, Cover = sCover },
                        Track = iTrack,
                        URL = $"https://music.163.com/song?id={iID}",
                        Details = [.. sDetail]
                    };
                }
            }
            catch (Exception ex)
            {
                sDetail.Add("Get title failed! \r\n EER: \r\n" + ex.Message);
                song = new Song() { Details = [.. sDetail] };
            }
            return (song);
        }

        // Get Song Lyric for multi-langiages 
        public async Task<string[]> GetSongLyric(int iID, bool CRLF = true)
        {

            List<string> sLRC = [];
            try
            {
                var uri = new Uri("http://music.163.com/api/song/media?id=" + iID);
                using var http = new HttpClient();
                var response = await http.GetAsync(uri);
                string sContent = await response.Content.ReadAsStringAsync();

                if (sContent[..4].Equals("ERR!"))
                {
                    sLRC.Add(string.Concat("Get lyric failed! \r\n EER: \r\n", sContent.AsSpan(4)));
                    return [.. sLRC];
                }

                JObject o = (JObject)JsonConvert.DeserializeObject(sContent);
                sLRC.Add(Strip(o["lyric"].ToString(), CRLF));
            }
            catch (Exception ex)
            {
                sLRC.Add("Get lyric failed! \r\n EER: \r\n" + ex.Message);
            }
            return [.. sLRC];
        }

        //Get Song Lyric with translated
        public async Task<string[]> GetSongLyricMultiLang(int iID)
        {
            List<string> sLRC = [];
            try
            {
                var uri = new Uri("http://music.163.com/api/song/lyric?os=pc&lv=-1&kv=-1&tv=-1&id=" + iID);
                using var http = new HttpClient();
                var response = await http.GetAsync(uri);
                string sContent = await response.Content.ReadAsStringAsync();

                if (sContent[..4].Equals("ERR!"))
                {
                    sLRC.Add(string.Concat("Get lyric failed! \r\n EER: \r\n", sContent.AsSpan(4)));
                    return [.. sLRC];
                }

                JObject o = (JObject)JsonConvert.DeserializeObject(sContent);

                if ((o.Property("uncollected") != null && (bool)o["uncollected"]) ||
                     (o.Property("nolyric") != null && (bool)o["nolyric"]))
                {
                    sLRC.Add("No Lyric Found!");
                    return ([.. sLRC]);
                }

                // Original Language Lyric
                if (o["lrc"]["lyric"] != null)
                {
                    string lyric = Strip( o["lrc"]["lyric"].ToString(), true );
                    if (lyric.Length > 0)
                    {
                        sLRC.Add(lyric);
                    }
                }
                // Translated Lyric
                if (o["tlyric"]["lyric"] != null)
                {
                    string tlyric = Strip( o["tlyric"]["lyric"].ToString(), true );
                    if (tlyric.Length > 0)
                    {
                        sLRC.Add(tlyric);
                    }
                }
                // KaraOk Lyric ?
                if (o["klyric"]["lyric"] != null)
                {
                    string klyric = Strip( o["klyric"]["lyric"].ToString(), true );
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
            return [.. sLRC];
        }

        // search music by title
        public async Task<MusicItem[]> GetMusicByTitle(string query, int offset = 0, int limit = 100, int type = 1)
        {
            List<MusicItem> sMusic = [];
            try
            {
                List<KeyValuePair<string, string>> postParams =
                [
                    new KeyValuePair<string, string>("offset", $"{offset}"),
                    new KeyValuePair<string, string>("limit", $"{limit}"),
                    new KeyValuePair<string, string>("type", "1"),
                    new KeyValuePair<string, string>("s", Uri.EscapeDataString(query).Replace("%20", "+")),
                ];

                var uri = new Uri("http://music.163.com/api/search/pc");
                using var http = new HttpClient();
                var content = new FormUrlEncodedContent(postParams);
                var response = await http.PostAsync(uri, content);
                string sContent = await response.Content.ReadAsStringAsync();

                if (sContent[..4].Equals("ERR!"))
                {
                    //sMusic.Add( "Search Music failed! \r\n EER: \r\n" + sContent.Substring( 4 ) );
                    return [.. sMusic];
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
                            MusicItem mItem = new()
                            {
                                title = m["name"].ToString()
                            };
                            if (m["alias"] != null)
                            {
                                List<string> aliasList = [];
                                foreach (JValue alias in m["alias"])
                                {
                                    aliasList.Add(alias.ToString());
                                }
                                mItem.title_alias = string.Join(" ; ", aliasList.ToArray());
                            }
                            mItem.id = m["id"].ToString();
                            List<string> arts = [];
                            List<string> photos = [];
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
                                List<string> aliasList = [];
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
            return [.. sMusic];
        }
    }
}
