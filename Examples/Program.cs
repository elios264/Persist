using System;
using System.Collections.Generic;
using System.Windows;
using elios.Persist;

namespace Examples
{
    public class Movie
    {
        public enum Genre { Action, Comedy, Terror, Thriller, Romantic, Sfi, Boring }

        public string Name { get; set; }
        public DateTime ReleaseDate { get; set; }

        [Persist(ValueName = "Likes")]
        public Dictionary<Genre,int> Genres { get; set; }

        public static Movie BadBoys => new Movie { Name = "Bad boys", ReleaseDate = DateTime.Now , Genres = new Dictionary<Genre, int> { { Genre.Action, 3 }, { Genre.Sfi, 21 } } };
    }


    class Program
    {
        static void Main(string[] args)
        {
            JsonArchive archive = new JsonArchive(typeof(Movie));

            var movieFile = "badboys.movie";

            //Serialize
            archive.Write(movieFile,Movie.BadBoys);
            //or
            ArchiveUtils.Write(movieFile, Movie.BadBoys, ArchiveFormat.Json);
            //or
            ArchiveUtils.WriteAnonymous(movieFile + ".json",Movie.BadBoys);

            //Deserialize
            var badBoysMovie = (Movie)archive.Read(movieFile);
            //or 
            var bboysMovie2 = ArchiveUtils.Read<Movie>(movieFile, ArchiveFormat.Json);
            //or
            var bboysMovie3 = ArchiveUtils.ReadAnonymous<dynamic>(movieFile, ArchiveFormat.Json);
            //or
            var bboysMovie4 = ArchiveUtils.ReadAnonymous<IDictionary<string,object>>(movieFile + ".json", ArchiveFormat.Guess);



            //Cyclic references
            Classroom c = new Classroom {Students = new List<Student>()};

            c.Students.Add(new Student {Name = "Alfred"});
            c.Students.Add(new Student {Name = "Ben"});
            c.Students.Add(new Student {Name = "Camila"});
            c.Students.Add(new Student {Name = "Denise"});

            var alfred = c.Students[0];
            var ben = c.Students[1];
            var camila = c.Students[2];
            var denise = c.Students[3];

            alfred.AddFriend(ben);
            alfred.AddFriend(camila);
            ben.AddFriend(alfred);
            ben.AddFriend(denise);
            camila.AddFriend(alfred);
            camila.AddFriend(ben);
            camila.AddFriend(denise);
            denise.AddFriend(camila);

            //Metdatadtypes
            c.RandomExternalTypeYouWantToSerialize = new Rect(4, 40, 23, 43);

            //Serialize
            XmlArchive xmlArchive = new XmlArchive(typeof(Classroom));
            var classroomFile = "classroom.xml";

            xmlArchive.Write(classroomFile, c);
            //or
            ArchiveUtils.Write(classroomFile, c);
            //this is not valid because this object uses references and metadatatypes
            //ArchiveUtils.WriteAnonymous(classroomFile, c);


            //Deserialize
            var classroom1 = (Classroom)xmlArchive.Read(classroomFile);
            //or
            var clasroom2 = ArchiveUtils.Read<Classroom>(classroomFile);



            //Polymorphic types
            Archive.LookForDerivedTypes = false;
            var yamlArchiveWithTypes = new YamlArchive(typeof(Automata), new[] { typeof(CommandTransition), typeof(ConditionTransition) });
            //or
            Archive.LookForDerivedTypes = true;
            var yamlArchive = new YamlArchive(typeof(Automata));

            var automataFile = "automata.yaml";

            //Serialize
            yamlArchive.Write(automataFile, Automata.SampleAutomata());
            //or
            ArchiveUtils.Write(automataFile, Automata.SampleAutomata(), ArchiveFormat.Guess);


            //Deserialize
            var automata1 = yamlArchive.Read(automataFile);
            //or
            var automata2 =(Automata) ArchiveUtils.Read(automataFile, typeof(Automata));


        }
    }
}
