  ____               _     _       _   _      _   
 |  _ \ ___ _ __ ___(_)___| |_    | \ | | ___| |_ 
 | |_) / _ \ '__/ __| / __| __|   |  \| |/ _ \ __|
 |  __/  __/ |  \__ \ \__ \ |_   _| |\  |  __/ |_ 
 |_|   \___|_|  |___/_|___/\__| (_)_| \_|\___|\__|


Persist is a .Net serializer/deserializer supporting  XML, Json & Yaml formats

Can Serialize/Deserialize:
	- Arbitrary custom types, native types, anonymous types, enums, IDictionary, IList, IConvertible
	- Multiply and possibly cyclical references to objects
	- Polymorphic objects
	- External types using metadata to specify the properties to use
	- Evolving types : you can add or remove properties/fields with no problem
	- Xml, Json, Yaml and any format you want because it is extensible
	- To/From a generic Node object that you can add/remove attributes and children
	- Using a PersistAttribute that allows you to:
		- Change the member name
		- Specify if serialize/deserialize as a reference
		- Ignore a member

Features:
	- Serialize and Deserialize any .Net object with Persist's powerful serializer
	- Since the metadata for the type is created on the constructor 
	serialization and deserialization is really fast
	- Persist is open source so you can contribute and completely free for comercial use
	- Create, parse, query and modify archives using Persist's Node and NodeAttribute objects
	- Serialization of objects with references is clearer and smaller than Newtonsoft Json.net library
	- Easy to use, comes with a static ArchiveUtils class with a lot of helper methods
	- if you need it Persist supports conversion between all of the 3 data formats ( xml, yaml, json)


    public class Movie
    {
        public enum Genre { Action, Comedy, Terror, Thriller, Romantic, Sfi, Boring }

        public string Name { get; set; }
        public DateTime ReleaseDate { get; set; }
        public List<Genre> Genres { get; }

        public static Movie BadBoys => new Movie 
		{
			Name = "Bad boys", 
			ReleaseDate = DateTime.Now, 
			Genres = new List<Genre> { Genre.Action, Genre.Comedy } 
		};
    }

	ArchiveUtils.Write("BadBoys.yaml", Movie.BadBoys);
    ArchiveUtils.Write("BadBoys.json", Movie.BadBoys, ArchiveFormat.Guess);
    ArchiveUtils.Write("BadBoys.movie", Movie.BadBoys, ArchiveFormat.Xml);

Yaml:

		Name: Bad boys
		ReleaseDate: 05/30/2016 01:08:14
		Genres:
		- value: Action
		- value: Comedy
		...

Json:
		{
		  "Name": "Bad boys",
		  "ReleaseDate": "05/30/2016 01:08:13",
		  "Genres": [
			{
			  "value": "Action"
			},
			{
			  "value": "Comedy"
			}
		  ]
		}

Xml:
		<Movie Name="Bad boys" ReleaseDate="05/30/2016 00:55:49">
		  <Genres>
			<Genre value="Action" />
			<Genre value="Comedy" />
		  </Genres>
		</Movie>


    var newMovie = ArchiveUtils.Read<Movie>("BadBoys.json");


Additional Info: 

	- Persist serializes by default public properties, if you want it to also serialize private 
	properties or fields they must specify the PersistAttribute in the declaration of the 
	member of the class or metadata class

	- When deserializing if a member has an instance that is already created it 
	will use that instance

	- Persist by default will run the parameterless constructor when deserializing objects,
	if you dont want this behaviour set the PersistAttribute.RunContructor 
	of the member to false.

	- the XmlArchive only reads attributes and child nodes with more attributes and 
	child nodes, meaning this is invalid : <node>text</node>

	- if you use a PersistAttribute like [Persist("")] on List & Dictionary members 
	using XmlArchive the container node will be ignored
	
	-The metadata types are looked in the same Assembly as the main type.




Backwards Compatibility:

		//BadBoys.movie
		<Movie Name="Bad boys" ReleaseDate="05/30/2016 01:08:14">
		  <Genres>
			<GENRE value="Action" />
			<GENRE value="Comedy" />
		  </Genres>
		</Movie>

Add this attribute:

		[Persist(ChildName = "GENRE")]
		public List<Genre> Genres { get; set; }

And there you have it:

		var movie = ArchiveUtils.Read<Movie>("BadBoys.movie");

LINQ to Archive:

		<Movie Name="Bad boys" ReleaseDate="05/30/2016 01:08:14">
		  <Genres>
			<GENRE value="Genre_Action" />
			<GENRE value="Genre_Comedy" />
		  </Genres>
		</Movie>

        var nodeToPatch = ArchiveUtils.LoadNode("BadBoys.movie");

        nodeToPatch.Nodes.Single(n => n.Name == "Genres").Nodes.ForEach(n =>
        {
            var attr = n.Attributes.Single(a => a.Name == "value");

            attr.Value = attr.Value.Replace("Genre_", "");
        } );

        var patchedMovie = ArchiveUtils.Read<Movie>(nodeToPatch);		


Serializing using Persist attributes:

	//Archive model
    public class Classroom
    {
        [Persist("",ChildName = "Student")]
        public List<Student> Students { get; set; }

		[Persist("ClassroomRect")]
        public System.Windows.Rect RandomExternalTypeYouWantToSerialize;
    }
    public class Student
    {
        [Persist("",IsReference = true, ChildName = "Friend")]
        private readonly List<Student> m_friends = new List<Student>;
		
		public string Name { get; set; }

		[Persist(Ignore = true)]
		public string StudentId  {get; set; }
    }

    [System.ComponentModel.DataAnnotations.MetadataType(typeof(Rect))]
    public class RectMeta
    {
        public double X { get; set; }
        public double Y { get; set; }

        [Persist("WIDTH")]
        public double Width { get; set; }
        public double Height { get; set; }
    }

	//Archive xml
	<Classroom>
	  <ClassroomRect X="15" Y="12" WIDTH="99" Height="45" />
	  <Student Name="Alfred" id="4">
		<Friend id="1" />
		<Friend id="2" />
	  </Student>
	  <Student Name="Ben" id="1">
		<Friend id="4" />
		<Friend id="5" />
	  </Student>
	  <Student Name="Camila" id="2">
		<Friend id="4" />
		<Friend id="1" />
		<Friend id="5" />
	  </Student>
	  <Student Name="Denise" id="5">
		<Friend id="2" />
	  </Student>
	</Classroom>

    var archive = new XmlArchive(typeof(Classroom));
	var newclassRoom = archive.Read("classroom.xml");