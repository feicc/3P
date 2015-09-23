﻿using System;
using System.Collections.Generic;
using _3PA.Lib;

namespace _3PA.MainFeatures.Parser {
    /// <summary>
    /// base abstract class for ParsedItem
    /// </summary>
    public abstract class ParsedItem {
        public string Name { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }
        public abstract void Accept(IParserVisitor visitor);
        protected ParsedItem(string name, int line, int column) {
            Name = name;
            Line = line;
            Column = column;
        }
    }

    /// <summary>
    /// Flags applicable for every ParsedItems
    /// </summary>
    [Flags]
    public enum ParseFlag {
        None = 1,
        Scope = 2,
        Global = 4,
        Parameter = 8,
        Reserved = 16,
        Abbreviation = 32,
        TempTable = 64
    }

    /// <summary>
    /// Mother of ParsedFunction and ParsedProcedure
    /// </summary>
    public abstract class ParsedScope : ParsedItem {
        public string LcName { get; private set; }
        protected ParsedScope(string name, int line, int column, string lcName) : base(name, line, column) {
            LcName = lcName;
        }
    }

    /// <summary>
    /// just to have a container for the global scope
    /// </summary>
    public class ParsedGlobal : ParsedScope {
        public override void Accept(IParserVisitor visitor) {
            visitor.Visit(this);
        }

        public ParsedGlobal(string name, int line, int column, string lcName) : base(name, line, column, lcName) {}
    }

    /// <summary>
    /// Function parsed item
    /// Flag : private
    /// </summary>
    public class ParsedFunction : ParsedScope {
        public string ReturnType { get; private set; }
        public string Parameters { get; private set; }
        public bool IsPrivate { get; private set; }
        public override void Accept(IParserVisitor visitor) {
            visitor.Visit(this);
        }

        public ParsedFunction(string name, int line, int column, string lcName, string returnType, string parameters, bool isPrivate) : base(name, line, column, lcName) {
            ReturnType = returnType;
            Parameters = parameters;
            IsPrivate = isPrivate;
        }
    }

    /// <summary>
    /// Procedure parsed item
    /// </summary>
    public class ParsedProcedure : ParsedScope {
        public string Left { get; private set; }
        public override void Accept(IParserVisitor visitor) {
            visitor.Visit(this);
        }

        public ParsedProcedure(string name, int line, int column, string lcName, string left) : base(name, line, column, lcName) {
            Left = left;
        }
    }

    /// <summary>
    /// include file parsed item
    /// </summary>
    public class ParsedIncludeFile : ParsedItem {
        public ParseScope Scope { get; private set; }
        public string LcOwnerName { get; private set; }

        public override void Accept(IParserVisitor visitor) {
            visitor.Visit(this);
        }

        public ParsedIncludeFile(string name, int line, int column, ParseScope scope, string lcOwnerName) : base(name, line, column) {
            Scope = scope;
            LcOwnerName = lcOwnerName;
        }
    }

    /// <summary>
    /// Pre-processed var parsed item
    /// </summary>
    public class ParsedPreProc : ParsedItem {
        public int UndefinedLine { get; set; }
        public ParsedPreProcFlag Flag { get; set; }
        public override void Accept(IParserVisitor visitor) {
            visitor.Visit(this);
        }

        public ParsedPreProc(string name, int line, int column, int undefinedLine, ParsedPreProcFlag flag) : base(name, line, column) {
            UndefinedLine = undefinedLine;
            Flag = flag;
        }
    }

    public enum ParsedPreProcFlag {
        None = 1,
        Scope = 2,
        Global = 4,
    }

    /// <summary>
    /// Define parsed item
    /// </summary>
    public class ParsedDefine : ParsedItem {
        public string FlagsStr { get; private set; }
        public string AsLike { get; private set; }
        public string Left { get; private set; }
        public ParseDefineType Type { get; private set; }
        public string PrimitiveType { get; private set; }
        public ParseScope Scope { get; private set; }
        public string LcOwnerName { get; private set; }
        public override void Accept(IParserVisitor visitor) {
            visitor.Visit(this);
        }

        public ParsedDefine(string name, int line, int column, string flagsStr, string asLike, string left, ParseDefineType type, string primitiveType, ParseScope scope, string lcOwnerName) : base(name, line, column) {
            FlagsStr = flagsStr;
            AsLike = asLike;
            Left = left;
            Type = type;
            PrimitiveType = primitiveType;
            Scope = scope;
            LcOwnerName = lcOwnerName;
        }
    }

    public enum ParseScope {
        Global,
        Procedure,
        Function
    }

    public class ParseDefineTypeAttr : Extensions.EnumAttr {
        public string Value { get; set; }
    }

    /// <summary>
    /// Enumeration of DEFINE types
    /// Retrieve the description value with ((ParseDefineTypeAttr)parseDefineType.GetAttributes()).Value
    /// </summary>
    public enum ParseDefineType {
        [ParseDefineTypeAttr(Value = "PARAMETER")]
        Parameter,
        [ParseDefineTypeAttr(Value = "DATA-SOURCE")]
        DataSource,
        [ParseDefineTypeAttr(Value = "EVENT")]
        Event,
        [ParseDefineTypeAttr(Value = "BUFFER")]
        Buffer,
        [ParseDefineTypeAttr(Value = "VARIABLE")]
        Variable,
        [ParseDefineTypeAttr(Value = "BROWSE")]
        Browse,
        [ParseDefineTypeAttr(Value = "STREAM")]
        Stream,
        [ParseDefineTypeAttr(Value = "BUTTON")]
        Button,
        [ParseDefineTypeAttr(Value = "DATASET")]
        Dataset,
        [ParseDefineTypeAttr(Value = "IMAGE")]
        Image,
        [ParseDefineTypeAttr(Value = "MENU")]
        Menu,
        [ParseDefineTypeAttr(Value = "FRAME")]
        Frame,
        [ParseDefineTypeAttr(Value = "QUERY")]
        Query,
        [ParseDefineTypeAttr(Value = "RECTANGLE")]
        Rectangle,
        [ParseDefineTypeAttr(Value = "PROPERTY")]
        Property,
        [ParseDefineTypeAttr(Value = "SUB-MENU")]
        SubMenu,
        [ParseDefineTypeAttr(Value = "NONE")]
        None,
    }

    /// <summary>
    /// Table or temp table parsed item
    /// </summary>
    public class ParsedTable : ParsedItem {
        public string Id { get; private set; }
        public string Crc { get; private set; }
        public string DumpName { get; private set; }
        public string Description { get; private set; }
        public ParseScope Scope { get; private set; }
        public string LcOwnerName { get; private set; }
        public string AsLike { get; private set; }
        public int Ranking { get; set; }
        public bool IsTempTable { get; private set; }
        public List<ParsedField> Fields { get; private set; }
        public List<ParsedIndex> Indexes { get; private set; }
        public List<ParsedTrigger> Triggers { get; private set; }
        public override void Accept(IParserVisitor visitor) {
            visitor.Visit(this);
        }

        public ParsedTable(string name, int line, int column, string id, string crc, string dumpName, string description, ParseScope scope, string lcOwnerName, string asLike, int ranking, bool isTempTable, List<ParsedField> fields, List<ParsedIndex> indexes, List<ParsedTrigger> triggers) : base(name, line, column) {
            Id = id;
            Crc = crc;
            DumpName = dumpName;
            Description = description;
            Scope = scope;
            LcOwnerName = lcOwnerName;
            AsLike = asLike;
            Ranking = ranking;
            IsTempTable = isTempTable;
            Fields = fields;
            Indexes = indexes;
            Triggers = triggers;
        }
    }

    /// <summary>
    /// describes a field of a table
    /// </summary>
    public class ParsedField {
        public string Name { get; private set; }
        public string Type { get;  set; }    
        public string Format { get;  set; }
        public int Order { get;  set; }
        public ParseFieldFlag Flag { get;  set; }
        public string InitialValue { get;  set; }
        public string Description { get;  set; }
        public string AsLike { get;  set; }
        public int Ranking { get;  set; }
        public ParsedField(string name, string type, string format, int order, ParseFieldFlag flag, string initialValue, string description, string asLike, int ranking) {
            Name = name;
            Type = type;
            Format = format;
            Order = order;
            Flag = flag;
            InitialValue = initialValue;
            Description = description;
            AsLike = asLike;
            Ranking = ranking;
        }
    }

    [Flags]
    public enum ParseFieldFlag {
        Mandatory = 1,
        Extent = 2,
        Index = 4,
        Primary = 8,
        None = 16
    }

    /// <summary>
    /// defines a index of a table
    /// </summary>
    public class ParsedIndex {
        public string Name { get; private set; }
        public ParseIndexFlag Flag { get; private set; }
        public List<string> FieldsList { get; private set; }
        public ParsedIndex(string name, ParseIndexFlag flag, List<string> fieldsList) {
            Name = name;
            Flag = flag;
            FieldsList = fieldsList;
        }
    }

    [Flags]
    public enum ParseIndexFlag {
        Primary = 1,
        Unique = 2,
    }

    /// <summary>
    /// defines a trigger of a table
    /// </summary>
    public class ParsedTrigger {
        public string Event { get; private set; }
        public string ProcName { get; private set; }
        public ParsedTrigger(string @event, string procName) {
            Event = @event;
            ProcName = procName;
        }
    }
}