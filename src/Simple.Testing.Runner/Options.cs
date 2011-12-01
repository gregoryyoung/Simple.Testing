//
// Options.cs
//
// Authors:
//  Jonathan Pryor <jpryor@novell.com>
//
// Copyright (C) 2008 Novell (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

// Compile With:
//   gmcs -debug+ -d:TEST -langversion:linq -r:System.Core Options.cs

//
// A Getopt::Long-inspired option parsing library for C#.
//
// Mono.Documentation.Options is built upon a key/value table, where the
// key is a option format string and the value is an Action<string>
// delegate that is invoked when the format string is matched.
//
// Option format strings:
//  BNF Grammar: ( name [=:]? ) ( '|' name [=:]? )+
// 
// Each '|'-delimited name is an alias for the associated action.  If the
// format string ends in a '=', it has a required value.  If the format
// string ends in a ':', it has an optional value.  If neither '=' or ':'
// is present, no value is supported.
//
// Options are extracted either from the current option by looking for
// the option name followed by an '=' or ':', or is taken from the
// following option IFF:
//  - The current option does not contain a '=' or a ':'
//  - The following option is not a registered named option
//
// The `name' used in the option format string does NOT include any leading
// option indicator, such as '-', '--', or '/'.  All three of these are
// permitted/required on any named option.
//
// Option bundling is permitted so long as:
//   - '-' is used to start the option group
//   - all of the bundled options do not require values
//   - all of the bundled options are a single character
//
// This allows specifying '-a -b -c' as '-abc'.
//
// Option processing is disabled by specifying "--".  All options after "--"
// are returned by Options.Parse() unchanged and unprocessed.
//
// Unprocessed options are returned from Options.Parse().
//
// Examples:
//  int verbose = 0;
//  Options p = new Options ()
//    .Add ("v", (v) => ++verbose)
//    .Add ("name=|value=", (v) => Console.WriteLine (v));
//  p.Parse (new string[]{"-v", "--v", "/v", "-name=A", "/name", "B", "extra"})
//    .ToArray ();
//
// The above would parse the argument string array, and would invoke the
// lambda expression three times, setting `verbose' to 3 when complete.  
// It would also print out "A" and "B" to standard output.
// The returned arrray would contain the string "extra".
//
// C# 3.0 collection initializers are supported:
//  var p = new Options () {
//    { "h|?|help", (v) => ShowHelp () },
//  };
//
// System.ComponentModel.TypeConverter is also supported, allowing the use of
// custom data types in the callback type; TypeConverter.ConvertFromString()
// is used to convert the value option to an instance of the specified
// type:
//
//  var p = new Options () {
//    { "foo=", (Foo f) => Console.WriteLine (f.ToString ()) },
//  };
//
// Random other tidbits:
//  - Boolean options (those w/o '=' or ':' in the option format string)
//    are explicitly enabled if they are followed with '+', and explicitly
//    disabled if they are followed with '-':
//      string a = null;
//      var p = new Options () {
//        { "a", (s) => a = s },
//      };
//      p.Parse (new string[]{"-a"});   // sets v != null
//      p.Parse (new string[]{"-a+"});  // sets v != null
//      p.Parse (new string[]{"-a-"});  // sets v == null
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;

namespace Simple.Testing.Runner {

	enum OptionValue {
		None, 
		Optional,
		Required
	}

	public class Option {
		string prototype, description;
		Action<string> action;
		string[] prototypes;
		OptionValue type;

		public Option (string prototype, string description, Action<string> action)
		{
			this.prototype = prototype;
			this.prototypes = prototype.Split ('|');
			this.description = description;
			this.action = action;
			this.type = GetOptionValue ();
		}

		public string Prototype { get { return prototype; } }
		public string Description { get { return description; } }
		public Action<string> Action { get { return action; } }

		internal string[] Prototypes { get { return prototypes; } }
		internal OptionValue OptionValue { get { return type; } }

		OptionValue GetOptionValue ()
		{
			foreach (string n in Prototypes) {
				if (n.IndexOf ('=') >= 0)
					return OptionValue.Required;
				if (n.IndexOf (':') >= 0)
					return OptionValue.Optional;
			}
			return OptionValue.None;
		}

		public override string ToString ()
		{
			return Prototype;
		}
	}

	public class Options : Collection<Option>
	{
	    readonly Dictionary<string, Option> _options = new Dictionary<string, Option> ();

		protected override void ClearItems ()
		{
			_options.Clear ();
		}

		protected override void InsertItem (int index, Option item)
		{
			Add (item);
			base.InsertItem (index, item);
		}

		protected override void RemoveItem (int index)
		{
			Option p = Items [index];
			foreach (string name in GetOptionNames (p.Prototypes)) {
				_options.Remove (name);
			}
			base.RemoveItem (index);
		}

		protected override void SetItem (int index, Option item)
		{
			RemoveItem (index);
			Add (item);
			base.SetItem (index, item);
		}

		public new Options Add (Option option)
		{
			foreach (string name in GetOptionNames (option.Prototypes)) {
				_options.Add (name, option);
			}
			return this;
		}

		public Options Add (string options, Action<string> action)
		{
			return Add (options, null, action);
		}

		public Options Add (string options, string description, Action<string> action)
		{
			Option p = new Option (options, description, action);
			base.Add (p);
			return this;
		}

		public Options Add<T> (string options, Action<T> action)
		{
			return Add (options, null, action);
		}

		public Options Add<T> (string options, string description, Action<T> action)
		{
			TypeConverter c = TypeDescriptor.GetConverter (typeof(T));
			Action<string> a = delegate (string s) {
				action (s != null ? (T) c.ConvertFromString (s) : default(T));
			};
			return Add (options, description, a);
		}

		static readonly char[] NameTerminator = new char[]{'=', ':'};
		static IEnumerable<string> GetOptionNames (string[] names)
		{
			foreach (string name in names) {
				int end = name.IndexOfAny (NameTerminator);
				if (end >= 0)
					yield return name.Substring (0, end);
				else 
					yield return name;
			}
		}

		static readonly Regex ValueOption = new Regex (
			@"^(?<flag>--|-|/)(?<name>[^:=]+)([:=](?<value>.*))?$");

        public List<string> Parse(IEnumerable<string> options)
		{
            var returnOptions = new List<string>();
			Option p = null;
			bool process = true;
			foreach (string option in options) {
				if (option == "--") {
					process = false;
					continue;
				}
				if (!process) {
					returnOptions.Add(option);
					continue;
				}
				Match m = ValueOption.Match (option);
				if (!m.Success) {
					if (p != null) {
						p.Action (option);
						p = null;
					}
					else
						returnOptions.Add(option);
				}
				else {
					string f = m.Groups ["flag"].Value;
					string n = m.Groups ["name"].Value;
					string v = !m.Groups ["value"].Success 
						? null 
						: m.Groups ["value"].Value;
					do {
						Option p2;
						if (_options.TryGetValue (n, out p2)) {
							p = p2;
							break;
						}
						// no match; is it a bool option?
						if (n.Length >= 1 && (n [n.Length-1] == '+' || n [n.Length-1] == '-') &&
								_options.TryGetValue (n.Substring (0, n.Length-1), out p2)) {
							v = n [n.Length-1] == '+' ? n : null;
							p2.Action (v);
							p = null;
							break;
						}
						// is it a bundled option?
						if (f == "-" && _options.TryGetValue (n [0].ToString (), out p2)) {
							int i = 0;
							do {
								if (p2.OptionValue != OptionValue.None)
									throw new InvalidOperationException (
											string.Format ("Unsupported using bundled option '{0}' that requires a value", n [i]));
								p2.Action (n);
							} while (++i < n.Length && _options.TryGetValue (n [i].ToString (), out p2));
						}

						// not a know option; either a value for a previous option
						if (p != null) {
							p.Action (option);
							p = null;
						}
						// or a stand-alone argument
						else
							returnOptions.Add(option);
					} while (false);
					if (p != null) {
						switch (p.OptionValue) {
							case OptionValue.None:
								p.Action (n);
								p = null;
								break;
							case OptionValue.Optional:
							case OptionValue.Required: 
								if (v != null) {
									p.Action (v);
									p = null;
								}
								break;
						}
					}
				}
			}
			if (p != null) {
				NoValue (ref p, "");
			}
            return returnOptions;
		}

		static void NoValue (ref Option p, string option)
		{
			if (p != null && p.OptionValue == OptionValue.Optional) {
				p.Action (null);
				p = null;
			}
			else if (p != null && p.OptionValue == OptionValue.Required) {
				throw new InvalidOperationException ("Expecting value after option " + 
					p.Prototype + ", found " + option);
			}
		}

		const int OptionWidth = 29;

		public void WriteOptionDescriptions (TextWriter o)
		{
			foreach (Option p in this) {
				List<string> names = new List<string> (GetOptionNames (p.Prototypes));

				int written = 0;
				if (names [0].Length == 1) {
					Write (o, ref written, "  -");
					Write (o, ref written, names [0]);
				}
				else {
					Write (o, ref written, "      --");
					Write (o, ref written, names [0]);
				}

				for (int i = 1; i < names.Count; ++i) {
					Write (o, ref written, ", ");
					Write (o, ref written, names [i].Length == 1 ? "-" : "--");
					Write (o, ref written, names [i]);
				}

				if (p.OptionValue == OptionValue.Optional)
					Write (o, ref written, "[=VALUE]");
				else if (p.OptionValue == OptionValue.Required)
					Write (o, ref written, "=VALUE");

				if (written < OptionWidth)
					o.Write (new string (' ', OptionWidth - written));
				else {
					o.WriteLine ();
					o.Write (new string (' ', OptionWidth));
				}

				o.WriteLine (p.Description);
			}
		}

		static void Write (TextWriter o, ref int n, string s)
		{
			n += s.Length;
			o.Write (s);
		}
	}
}