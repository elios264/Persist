using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows;
using elios.Persist;

namespace Examples
{

    // converters support.
    public class MovieConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var str = value as string;

            return str != null
                ? Movie.BadBoys
                : base.ConvertFrom(context, culture, value);
        }
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            return destinationType == typeof(string)
                ? "BadBoys"
                : base.ConvertTo(context, culture, value, destinationType);
        }
    }



    // if you do not have access to modify the original type you can use the metadatypeattribute instead
    [MetadataType(typeof(Rect))]
    public class RectMeta
    {
        public double X { get; set; }
        public double Y { get; set; }

        // if the xml/json/yaml comes in other with other naming different from the class you want to use can specify an alias 
        [Persist("WIDTH")] 
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class Student
    {
        //you can also deserialize to private fields just need to add the persist attribute
        // for lists & dictionaries if you specify IsReference to true, a reference to the value is going to be saved instead
        //if you set an empty name for the peristattribute now you have an anonymous container ( this is valid only for xml serialization )
        //use childname to overwrite the default behaviour that is to use the name of the class (Student) 
        [Persist("",IsReference = true, ChildName = "Friend")]
        private readonly List<Student> m_friends;

        //public properties are serialized by default
        public string Name { get; set; }


        //constructors are called by default
        //even if m_friends was not initialized and/or readonly it'll still deserialize correctly
        public Student()
        {
            m_friends = new List<Student>();
        }

        public void AddFriend(Student friend)
        {
            m_friends.Add(friend);
        }
    }
    public class Classroom
    {
        //Runconstructor to false will make persist use FormatterServices.GetUninitializedObject instead of Activator.CreateInstance 
        [Persist("",ChildName = "Student", RunConstructor = false)]
        public List<Student> Students { get; set; }

        //again, alias for your property because it differs from the xml/yaml/json
        [Persist("ClassroomRect")]
        public System.Windows.Rect RandomExternalTypeYouWantToSerialize = new Rect(15,12,0,45);

        public static Classroom SampleClassroom()
        {
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

            return c;
        }
    }

    // to allow Persist to recognize additional types when serializing/deserializing
    [PersistInclude(typeof(CommandTransition),typeof(ConditionTransition))]
    public class Transition
    {
        public string Name { get; set; }
    }

    public class ConditionTransition : Transition
    {
        public string Condition { get; set; }
    }

    public class CommandTransition : Transition
    {
        public int Command { get; set; }
    }

    public class State
    {
        public string Name { get; set; }
        
        //this is a polymorphic member since Transition is the base class of command & condition transition
        //so you need to do something like this: var serializer = new XmlArchive(typeof(State),new [] { typeof(ConditionTransition), typeof(CommandTransition) });
        //or set the global setting Archive.LookForDerivedTypes = true;  this will look for all the derived types in the current domain
        [Persist(ChildName = "tt")]
        public List<Transition> Transitions { get; set; } 
    }


    public class Automata
    {
        //this prop is a reference, so you need to serialize it somewhere else.
        [Persist(IsReference = true)]
        public State InitialState { get; set; }

        //since this is a only getter property it will deserialize only if the prop is initialized in the constructor
        // otherwise it'll ignore it
        public List<State> States{ get; }


        public Automata()
        {
            States = new List<State>();
        }

        public static Automata SampleAutomata()
        {
            Automata a = new Automata();


            a.States.Add(new State
            {
                Name = "Standing",
                Transitions = new List<Transition>
                {
                    new Transition {Name = "Transition" },
                    new CommandTransition {Name = "Command1", Command = 5456},
                    new ConditionTransition {Name = "Cond1", Condition = "is_falling" },
                    new CommandTransition {Name = "Command2", Command = 22},
                    new CommandTransition {Name = "Command3", Command = 533}
                }
            });

            a.States.Add(new State
            {
                Name = "Happy",
                Transitions = new List<Transition>
                {
                    new Transition {Name = "Transition" },
                    new ConditionTransition {Name = "Cond3", Condition = "is_high" },
                    new ConditionTransition {Name = "Cond4", Condition = "is_drunk" }
                }
            });

            a.States.Add(new State
            {
                Name = "Drunk",
                Transitions = new List<Transition>
                {
                    new CommandTransition {Name = "Command1", Command = 22},
                    new ConditionTransition {Name = "Intoxicate", Condition = "is_still_drinking" }
                }
            });

            a.States.Add(new State
            {
                Name = "Intoxicated",
                Transitions = new List<Transition>
                {
                    new ConditionTransition {Name = "Cond1", Condition = "is_falling" }
                }
            });

            a.InitialState = a.States[0];

            return a;
        }
    }

}