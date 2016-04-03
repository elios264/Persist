using System.Collections.Generic;
using PersistDotNet.Persist;

namespace Examples
{

    public class Person
    {
        [Persist("Friends",IsReference = true, ChildName = "Friend")]
        private readonly List<Person> m_friends;

        public string Name { get; set; }

        public Person()
        {
            m_friends = new List<Person>();
        }

        public void AddFriend(Person friend)
        {
            m_friends.Add(friend);
        }
    }
    public class Classroom
    {
        [Persist("",ChildName = "Student")]
        public List<Person> Students { get; set; }


        public static Classroom SampleClassroom()
        {
            Classroom c = new Classroom {Students = new List<Person>()};

            c.Students.Add(new Person {Name = "Alfred"});
            c.Students.Add(new Person {Name = "Ben"});
            c.Students.Add(new Person {Name = "Camila"});
            c.Students.Add(new Person {Name = "Denise"});

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
        public List<Transition> Transitions { get; set; } 
    }


    public class Automata
    {
        [Persist(IsReference = true)]
        public State InitialState { get; set; }

        [Persist("")]
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
                    new CommandTransition {Name = "Command1", Command = 5456},
                    new ConditionTransition {Name = "Cond1", Condition = "is_falling" },
                    new CommandTransition {Name = "Command2", Command = 22},
                    new CommandTransition {Name = "Command3", Command = 533},
                }
            });

            a.States.Add(new State
            {
                Name = "Happy",
                Transitions = new List<Transition>
                {
                    new ConditionTransition {Name = "Cond3", Condition = "is_high" },
                    new ConditionTransition {Name = "Cond4", Condition = "is_drunk" },
                }
            });

            a.States.Add(new State
            {
                Name = "Drunk",
                Transitions = new List<Transition>
                {
                    new CommandTransition {Name = "Command1", Command = 22},
                    new ConditionTransition {Name = "Intoxicate", Condition = "is_still_drinking" },
                }
            });

            a.States.Add(new State
            {
                Name = "Intoxicated",
                Transitions = new List<Transition>
                {
                    new ConditionTransition {Name = "Cond1", Condition = "is_falling" },
                }
            });

            a.InitialState = a.States[0];

            return a;
        }
    }

}
