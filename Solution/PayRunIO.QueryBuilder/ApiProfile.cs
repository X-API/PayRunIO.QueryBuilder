namespace PayRunIO.QueryBuilder
{
    using System;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Windows;
    using System.Xml.Serialization;

    using PayRunIO.CSharp.SDK;

    public class ApiProfiles
    {
        private const string DefaultProfileName = "Default";

        private static ApiProfiles instance;

        private ApiProfiles(ApiProfile[] profiles)
        {
            profiles = profiles ?? new ApiProfile[0];

            this.Profiles = new ObservableCollection<ApiProfile>(profiles);

            if (!this.Profiles.Any())
            {
                this.CreateDefaultProfile();
                this.SelectedProfile = this[DefaultProfileName];
                this.Save();
                return;
            }

            if (this.SelectedProfile == null)
            {
                this.SelectedProfile = this.Profiles.First();
            }
        }

        private void CreateDefaultProfile()
        {
            this.AddProfile(DefaultProfileName);
            this[DefaultProfileName].ApiHostUrl = "https://api.test.payrun.io";
        }

        public static ApiProfiles Instance
        {
            get
            {
                instance = instance ?? new ApiProfiles(LoadProfiles());
                return instance;
            }

            set => instance = value;
        }

        public ObservableCollection<ApiProfile> Profiles { get; } = new ObservableCollection<ApiProfile>();

        public ApiProfile this[string name] 
        { 
            get
            {
                return this.Profiles?.FirstOrDefault(p => p.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        public ApiProfile SelectedProfile
        {
            get => this[AppSettings.Default.LastProfileName];
            set => AppSettings.Default.LastProfileName = value?.Name;
        }

        public void AddProfile(string name)
        {
            if (this[name] == null)
            {
                var profile = new ApiProfile { Name = name };

                this.Profiles.Add(profile);
            }
        }

        public void DeleteProfile(string name)
        {
            var profileToDelete = this[name];

            if (profileToDelete != null)
            {
                this.Profiles.Remove(profileToDelete);
            }
        }

        public void Save()
        {
            using (var stream = XmlSerialiserHelper.Serialise(this.Profiles.ToArray()))
            {
                using (var sr = new StreamReader(stream))
                {
                    AppSettings.Default.ApiProfiles = sr.ReadToEnd();
                }
            }

            AppSettings.Default.Save();
        }

        private static ApiProfile[] LoadProfiles()
        {
            var source = AppSettings.Default.ApiProfiles;

            var profiles = new ApiProfile[0];

            if (!string.IsNullOrEmpty(source))
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(source)))
                {
                    try
                    {
                        profiles = XmlSerialiserHelper.DeserialiseArrayStream<ApiProfile>(ms);
                    }
                    catch (Exception)
                    {
                        // Exception sink - Protects from corrupt setting file data.
                    }
                }
            }

            return profiles;
        }
    }

    public class ApiProfile
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string ApiHostUrl { get; set; }

        [XmlAttribute]
        public string ConsumerKey { get; set; }

        [XmlAttribute]
        public string ConsumerSecret { get; set; }

        [XmlAttribute]
        public string ResponseType { get; set; }
    }
}
