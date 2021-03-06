﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using elios.Persist;

namespace Examples
{
    //[TypeConverter(typeof(MovieConverter))]
    public class Movie
    {
        public enum Genre { Action, Comedy, Terror, Thriller, Romantic, Sfi, Boring }

        public string Name { get; set; }
        public DateTime ReleaseDate { get; set; }

        [Persist(ValueName = "Likes", ChildName = "Genre")]
        public Dictionary<Genre,int> Genres { get; set; }

        public static Movie BadBoys => new Movie { Name = "Bad boys", ReleaseDate = DateTime.Now , Genres = new Dictionary<Genre, int> { { Genre.Action, 3 }, { Genre.Sfi, 21 } } };
    }

    //[MetadataType(typeof(Movie))]
    //[TypeConverter(typeof(MovieConverter))]
    public class MovieMeta {}


    class Program
    {
        static void Main(string[] args)
        {
            var archive = new JsonArchive(typeof(Movie));

            var movieFile = "badboys.movie";

            //Serialize
            archive.Write(movieFile, Movie.BadBoys);
            //or
            ArchiveUtils.Write(movieFile, Movie.BadBoys, ArchiveFormat.Json);

            //Deserialize
            var badBoysMovie = (Movie)archive.Read(movieFile);
            //or 
            var bboysMovie2 = ArchiveUtils.Read<Movie>(movieFile);
            //or
            var bboysMovie3 = ArchiveUtils.LoadNode(movieFile, ArchiveFormat.Json);

            //Serializing & Deserializing runtime objects
            XmlArchive arc = new XmlArchive(null);

            arc.Write("runtimeObject.xml", new Dictionary<string, object>
            {
                {"Genre", Movie.Genre.Action },
                {"Amount", 2323 },
                {"Producer", Automata.SampleAutomata() },
            }, "Dictionary");

            var obje = new XmlArchive(null).Read("runtimeObject.xml");


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



            //Deserialize
            var classroom1 = (Classroom)xmlArchive.Read(classroomFile);
            //or
            var clasroom2 = ArchiveUtils.Read<Classroom>(classroomFile);



            //Polymorphic types
            var yamlArchiveWithExplicitTypes = new YamlArchive(typeof(Automata), typeof(CommandTransition), typeof(ConditionTransition));
            //or using in the subclass needing them [PersistInclude(typeof(CommandTransition),typeof(ConditionTransition))]
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
