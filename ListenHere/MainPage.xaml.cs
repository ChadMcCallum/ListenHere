using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using AsyncOAuth;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using ListenHere.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Windows.Devices.Geolocation;

namespace ListenHere
{
    public partial class MainPage : PhoneApplicationPage
    {
        private WebClient client;
        private string _echonestApiKey;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            client = new WebClient();
            StartProcess();
        }

        private async void StartProcess()
        {
            //get location
            var locator = new Geolocator();
            locator.DesiredAccuracyInMeters = 50;
            locator.MovementThreshold = 5;
            locator.ReportInterval = 500;
            var location = await locator.GetGeopositionAsync(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
            Output.WriteLine("Current location: {0},{1}", location.Coordinate.Latitude, location.Coordinate.Longitude);
            //get city, region, country from bing
            var url =
                string.Format(
                    "http://dev.virtualearth.net/REST/v1/Locations/{0},{1}?includeEntityTypes={2}&includeNeighborhood={3}&key={4}",
                    location.Coordinate.Latitude, location.Coordinate.Longitude, "PopulatedPlace,Postcode1,AdminDivision1,CountryRegion", "0",
                    "AsLS7O-r6pfOy34GIpb9uaRE-XwsBQRdoi67Iw4JnWzPh10sMesSEo8T_bdYbXUx");
            Output.WriteLine("Downloading address data from bing: {0}", url);
            var bingClient = new WebClient();
            bingClient.DownloadStringCompleted += BingClientOnDownloadStringCompleted;
            bingClient.DownloadStringAsync(new Uri(url));
        }

        private void BingClientOnDownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            dynamic location = JObject.Parse(e.Result);
            dynamic address = location.resourceSets[0].resources[0].address;
            Output.WriteLine("City: {0}, Region: {1}, Country: {2}", (string)address.locality, (string)address.adminDistrict2, (string)address.countryRegion);
            //search echonest api for artists
            _echonestApiKey = "JZN9STIQ4WTPTFXTQ";
            var url = string.Format("http://developer.echonest.com/api/v4/artist/search?api_key={0}&format=json&artist_location=city:{1}+region:{2}+country:{3}&bucket=artist_location",
                _echonestApiKey, (string)address.locality, (string)address.adminDistrict2, (string)address.countryRegion);
            Output.WriteLine("Searching for artists from {0}", url);
            var artistSearchClient = new WebClient();
            artistSearchClient.DownloadStringCompleted += ArtistSearchClientOnDownloadStringCompleted;
            artistSearchClient.DownloadStringAsync(new Uri(url));
        }

        private void ArtistSearchClientOnDownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            dynamic artists = JObject.Parse(e.Result);
            var builder = new StringBuilder();
            var count = 0;
            foreach (var artist in artists.response.artists)
            {
                Output.WriteLine("Found artist: {0} ({1})", (string)artist.name, (string)artist.id);
                if (count < 5)
                {
                    builder.AppendFormat("artist_id={0}&", artist.id);
                    count++;
                }
            }

            //get playlist
            var url =
                string.Format(
                    "http://developer.echonest.com/api/v4/playlist/basic?api_key={0}&{1}format=json&results=20&bucket=id:7digital-US&bucket=tracks&limit=true",
                    _echonestApiKey, builder);
            Output.WriteLine("Getting a playlist from {0}", url);
            var playlistClient = new WebClient();
            playlistClient.DownloadStringCompleted += PlaylistClientOnDownloadStringCompleted;
            playlistClient.DownloadStringAsync(new Uri(url));
        }

        private void PlaylistClientOnDownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            dynamic playlist = JObject.Parse(e.Result);
            var song = playlist.response.songs[0];
            var track = playlist.response.songs[0].tracks[0];
            Output.WriteLine("Found mp3: {0} by {1} ({2})", (string)song.title, (string)song.artist_name, (string)track.foreign_id);
            Preview.Source = new Uri((string)track.preview_url);
            var id = ((string) track.foreign_id).Replace("7digital-US:track:", "");
            
        }
    }

    public static class TextBlockExtensions
    {
        public static void WriteLine(this TextBlock block, string format, params object[] args)
        {
            block.Text = block.Text + string.Format(format + "\n\n", args);
        }
    }

}