using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Text.Unicode;

namespace Xojo_Converter
{

    struct blockHeader
    {
        public int typeid;
        public int id;
        public int revision;
        public int blocksize;
        public int blockKeyFormat;
        public int key1;
        public int key2;

    }

    struct format1header
    {
        public int signature;
        public int formatversion;
        public int unknown1;
        public int unknown;
        public int firstblock;
    }

    struct format2header
    {
        public int signature;
        public int formatversion;
        public int unknown1;
        public int unknown;
        public int firstblock;
        public int unknown3;
        public int minversion;
    }

    public class EndiannessAwareBinaryReader : BinaryReader
    {

        public enum Endianness
        {
            Little,
            Big,
        }

        private readonly Endianness _endianness = Endianness.Little;

        public EndiannessAwareBinaryReader(Stream input) : base(input)
        {
        }

        public EndiannessAwareBinaryReader(Stream input, Encoding encoding) : base(input, encoding)
        {
        }

        public EndiannessAwareBinaryReader(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
        {
        }

        public EndiannessAwareBinaryReader(Stream input, Endianness endianness) : base(input)
        {
            _endianness = endianness;
        }

        public EndiannessAwareBinaryReader(Stream input, Encoding encoding, Endianness endianness) : base(input, encoding)
        {
            _endianness = endianness;
        }

        public EndiannessAwareBinaryReader(Stream input, Encoding encoding, bool leaveOpen, Endianness endianness) : base(input, encoding, leaveOpen)
        {
            _endianness = endianness;
        }

        public override short ReadInt16() => ReadInt16(_endianness);

        public override int ReadInt32() => ReadInt32(_endianness);

        public override long ReadInt64() => ReadInt64(_endianness);

        public override ushort ReadUInt16() => ReadUInt16(_endianness);

        public override uint ReadUInt32() => ReadUInt32(_endianness);

        public override ulong ReadUInt64() => ReadUInt64(_endianness);

        public override double ReadDouble() => ReadDouble(_endianness);

        public double ReadDouble(Endianness endianness) => BitConverter.ToDouble(ReadForEndianness(sizeof(double), endianness));

        public short ReadInt16(Endianness endianness) => BitConverter.ToInt16(ReadForEndianness(sizeof(short), endianness));

        public int ReadInt32(Endianness endianness) => BitConverter.ToInt32(ReadForEndianness(sizeof(int), endianness));

        public long ReadInt64(Endianness endianness) => BitConverter.ToInt64(ReadForEndianness(sizeof(long), endianness));

        public ushort ReadUInt16(Endianness endianness) => BitConverter.ToUInt16(ReadForEndianness(sizeof(ushort), endianness));

        public uint ReadUInt32(Endianness endianness) => BitConverter.ToUInt32(ReadForEndianness(sizeof(uint), endianness));

        public ulong ReadUInt64(Endianness endianness) => BitConverter.ToUInt64(ReadForEndianness(sizeof(ulong), endianness));

        private byte[] ReadForEndianness(int bytesToRead, Endianness endianness)
        {
            var bytesRead = ReadBytes(bytesToRead);

            if ((endianness == Endianness.Little && !BitConverter.IsLittleEndian)
                || (endianness == Endianness.Big && BitConverter.IsLittleEndian))
            {
                Array.Reverse(bytesRead);
            }

            return bytesRead;
        }
    }


    public class RBBFConverter : IRBBFConverter
    {
        public const int kSuccess = 1;
        public const int kFailure = 0;
        public const int kUTF8 = 134217984;

        private Dictionary<string, string> blockTags;
        private Dictionary<string, string> Tags;

        //  StreamWriter outputStream;
        private BufferedStreamWriter outputStream;

        private List<string> mBufferedLines;
        private string _mVersion;

        private string mVersion
        {
            get { return _mVersion; }
            set
            {
                _mVersion = value;
                foreach (var line in mBufferedLines)
                {
                    string tmp = line.Replace("\"(mVersion)\"", "\""+ value + "\"");
                    outputStream.WriteLine(tmp);
                }
            }
        }

        public RBBFConverter()
        {
            _mVersion = "";
            mBufferedLines = new List<string>();
        }

        private int fourCharCode(string letters)
        {
            int value;
            value = 0;

            int tmp;
            tmp = letters[0] & 0xFF;
            value |= tmp << 24;
            tmp = letters[1] & 0xFF;
            value |= tmp << 16;
            tmp = letters[2] & 0xFF;
            value |= tmp << 8;
            tmp = letters[3] & 0xFF;
            value |= tmp << 0;

            return value;

        }

        public void ConvertFile(string inputFilePath, string outputFilePath)
        {

            try
            {
                if (!System.IO.File.Exists(inputFilePath))
                {
                    Console.WriteLine("Input file does not exist");
                    return;
                }
                using (FileStream inputStream = System.IO.File.OpenRead(inputFilePath))
                {

                    if (string.IsNullOrEmpty(outputFilePath) || string.IsNullOrWhiteSpace(outputFilePath))
                    {
                        /***
                         // get std out as a StreamWriter so we can refer to it using one local
                        outputStream = new StreamWriter(Console.OpenStandardOutput());
                        // make sure its set to auto flush
                        outputStream.AutoFlush = true;
                        // make sure console uses it
                        Console.SetOut(outputStream);
                        ***/
                        outputStream = new BufferedStreamWriter();
                    }
                    else
                    {
                        if (System.IO.File.Exists(outputFilePath))
                        {
                            System.IO.File.Delete(outputFilePath);
                        }
                        // outputStream = new StreamWriter(File.Create(outputFilePath));
                        outputStream = new BufferedStreamWriter(System.IO.File.Create(outputFilePath));

                    }

                    EndiannessAwareBinaryReader bis;
                    bis = new EndiannessAwareBinaryReader(inputStream, Encoding.UTF8, true, EndiannessAwareBinaryReader.Endianness.Big);

                    // file header

                    format2header header;

                    header.signature = bis.ReadInt32();
                    header.formatversion = bis.ReadInt32();
                    header.unknown1 = bis.ReadInt32();
                    header.unknown = bis.ReadInt32();
                    header.firstblock = bis.ReadInt32();
                    header.minversion = 201201;

                    if (2 == header.formatversion)
                    {
                        header.minversion = bis.ReadInt32();
                    }

                    loadTags(header.formatversion);

                    bis.BaseStream.Position = header.firstblock;

                    outputWrite("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                    string outstr = string.Format("<RBProject version=\"(mVersion)\" FormatVersion=\"{0}\" MinIDEVersion=\"{1}\">", header.formatversion, header.minversion);
                    outputWrite(outstr);

                    while (bis.PeekChar() != -1)
                    {
                        // Dim blockTag As Int32 = bis.ReadInt32
                        int blockTag;
                        blockTag = bis.ReadInt32();
                        string blockTagStr;
                        blockTagStr = fourCharAsString(blockTag);

                        switch (blockTagStr)
                        {

                            case "Blok":
                                int tmp = fourCharCode("Blok");
                                if (kSuccess != processBlocks(bis))
                                {
                                    return;

                                }

                                break;

                            case "EOF!":
                                // Exit While
                                break;

                            default:
                                Console.WriteLine($" unhandled blok tag type {blockTagStr}");
                                System.Environment.Exit(-1);
                                break;
                        }
                    }

                    outputWrite("</RBProject>");

                    // End Try

                    bis.Close();
                    bis.Dispose();
                    outputStream.Dispose();
                }
            }
            catch
            {
                if (1 == 2)
                {
                    // we got major issues !!!!!
                }
            }
        }

        private int processBlocks(EndiannessAwareBinaryReader bis)
        {


            blockHeader blockHead;

            blockHead.typeid = bis.ReadInt32();
            blockHead.id = bis.ReadInt32();
            blockHead.revision = bis.ReadInt32();
            blockHead.blocksize = bis.ReadInt32();
            blockHead.blockKeyFormat = bis.ReadInt32();
            blockHead.key1 = bis.ReadInt32();
            blockHead.key2 = bis.ReadInt32();

            string blockTypeStr;
            blockTypeStr = convertRBBFBlockTagToXMLBlockTag(blockHead.typeid);

            byte[] data;
            data = bis.ReadBytes(blockHead.blocksize - 32);

            try
            {
                if (processOneBlock(blockTypeStr, blockHead, data) != kSuccess)
                {
                    return kFailure;
                }
            }
            catch
            {
                if (1 == 2) { }
            }

            return kSuccess;
        }

        private byte[] BlockHeadAsBigEndian(blockHeader blockHead)
        {
            byte[] typeIDBytes = BitConverter.GetBytes(blockHead.typeid);
            bool isLittle = BitConverter.IsLittleEndian;
            if (true == BitConverter.IsLittleEndian)
            {
                Array.Reverse(typeIDBytes);
            }
            byte[] idBytes = BitConverter.GetBytes(blockHead.id);
            if (true == BitConverter.IsLittleEndian)
            {
                Array.Reverse(idBytes);
            }
            byte[] revisionBytes = BitConverter.GetBytes(blockHead.revision);
            if (true == BitConverter.IsLittleEndian)
            {
                Array.Reverse(revisionBytes);
            }
            byte[] blocksizeBytes = BitConverter.GetBytes(blockHead.blocksize);
            if (true == BitConverter.IsLittleEndian)
            {
                Array.Reverse(blocksizeBytes);
            }
            byte[] blockKeyFormatBytes = BitConverter.GetBytes(blockHead.blockKeyFormat);
            if (true == BitConverter.IsLittleEndian)
            {
                Array.Reverse(blockKeyFormatBytes);
            }
            byte[] key1Bytes = BitConverter.GetBytes(blockHead.key1);
            if (true == BitConverter.IsLittleEndian)
            {
                Array.Reverse(key1Bytes);
            }
            byte[] key2Bytes = BitConverter.GetBytes(blockHead.key2);
            if (true == BitConverter.IsLittleEndian)
            {
                Array.Reverse(key2Bytes);
            }

            return Combine(typeIDBytes, Combine(idBytes, Combine(revisionBytes, Combine(blocksizeBytes, Combine(blockKeyFormatBytes, Combine(key1Bytes, key2Bytes)))))) ;
        }

        public byte[] GetBytes(object obj)
        {

            int len = Marshal.SizeOf(obj);

            byte[] arr = new byte[len];

            IntPtr ptr = Marshal.AllocHGlobal(len);

            Marshal.StructureToPtr(obj, ptr, true);

            Marshal.Copy(ptr, arr, 0, len);

            Marshal.FreeHGlobal(ptr);

            return arr;

        }

        public static byte[] Combine(byte[] first, byte[] second)
        {
            byte[] bytes = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, bytes, 0, first.Length);
            Buffer.BlockCopy(second, 0, bytes, first.Length, second.Length);
            return bytes;
        }

        private int processOneBlock(string blockTypeStr, blockHeader blockHead, byte[] data)
        {
            if (blockTypeStr == "Module" && blockHead.id == 1583173631)
            {
                if (1 == 2) { }
            }

            string outstr = string.Format("<block type=\"{0}\" ID=\"{1}\">", blockTypeStr, blockHead.id);
            outputWrite(outstr);

            if (blockTypeStr == "Project")
            {
                blockHead.key1 = 0;
                blockHead.key2 = 0;
            }

            if (blockHead.blockKeyFormat != 0)
            {

                string value;
                byte[] blokBytes = new byte[] { 0x42, 0x6C, 0x6F, 0x6B }; // Blok

                if (1 == 2) { }
                // header is gotten in normal intel big endian form but we need it in LITTLE endian format !
                value = MakeHexBytesValue(Combine(blokBytes, Combine(BlockHeadAsBigEndian(blockHead), data)));

                outputWrite(value);
            }
            else
            {

                bool watchForPSIV = false;

                if ("Project" == blockTypeStr)
                {
                    watchForPSIV = true;
                }

                // ok the mb we're handed we can use to back a binary stream !
                EndiannessAwareBinaryReader bis;
                bis = new EndiannessAwareBinaryReader(new MemoryStream(data), Encoding.UTF8, true, EndiannessAwareBinaryReader.Endianness.Big);
                // ????? bis.LittleEndian = data.LittleEndian;
                while (bis.BaseStream.Position < bis.BaseStream.Length)
                {
                    // read a tag
                    int tag = bis.ReadInt32();
                    string tagStr;
                    tagStr = fourCharAsString(tag);

                   

                    // certain tags are handled specially
                    if (false == processGroupItem(tag, bis))
                    {
                        // and its value type
                        int typetag;
                        typetag = bis.ReadInt32();
                        string typeTagChars;
                        typeTagChars = fourCharAsString(typetag);


                        string value = ConvertTypeToString(bis, typetag);
                        if (true == ContainsLowBytes(value))
                        {
                            try
                            {
                                value = MakeHexBytesValue(value);
                            }
                            catch
                            {
                                if (1 == 2)
                                {
                                    // we got major issues !!!!!
                                }
                            }
                        }
                        else
                        {
                            value = MakeXMLSafe(value);
                        }

                        string xmlTag;
                        xmlTag = ConvertRBBFTagToXMLTag(tag);

                        if ("" != xmlTag)
                        {
                            outputWrite("<" + xmlTag + ">" + value + "</" + xmlTag + ">");
                        }


                        if (true == watchForPSIV && tag == fourCharCode("PSIV"))
                        {
                            mVersion = translatePSIVToVersion(value);
                        }
                    }
                }

                // didnt find one ?
                if (true == watchForPSIV)
                {
                    try
                    {
                        mVersion = translatePSIVToVersion("2005.01.01");
                    }
                    catch {
                        if (1 == 2)
                        {
                            // we got major issues !!!!!
                        }
                    }
                }
            }

            outputWrite("</block>");

            return kSuccess;
        }

        private bool skipGroup(EndiannessAwareBinaryReader bis, string groupName)
        {
            // and its value type
            int grouptypetag = bis.ReadInt32(); // ALWAYS Grup !
            string grouptypeTagChars;
            grouptypeTagChars = fourCharAsString(grouptypetag);
            if (grouptypetag != fourCharCode("Grup"))
            {
                bis.BaseStream.Position = bis.BaseStream.Position - 4;
                return false;
            }


            int size = bis.ReadInt32();
            int id = bis.ReadInt32();
            int dataSize;
            dataSize = size - 4;

            // outputWrite("<" + groupName + ">")

            int propNameTag = bis.ReadInt32();
            string propNameTagChars = fourCharAsString(propNameTag);


            while (propNameTag != fourCharCode("EndG"))
            {
                // certain tags are handled specially
                if (false == processGroupItem(propNameTag, bis))
                {
                    // and its value type
                    int typetag = bis.ReadInt32();
                    string typeTagChars;
                    typeTagChars = fourCharAsString(typetag);


                    string value = ConvertTypeToString(bis, typetag);


                    if (ContainsLowBytes(value))
                    {
                        value = MakeHexBytesValue(value);
                    }
                    else if (typetag == fourCharCode("Rect"))
                    {
                        //
                    }
                    else
                    {
                        value = MakeXMLSafe(value);
                    }

                    // Dim xmlTag As String

                    // xmlTag = ConvertRBBFTagToXMLTag(propNameTag)

                    // outputWrite("<" + xmlTag + ">" + value + "</" + xmlTag + ">")

                }

                propNameTag = bis.ReadInt32();
                propNameTagChars = fourCharAsString(propNameTag);

            }

            int endtype = bis.ReadInt32();
            if (endtype != fourCharCode("Int "))
            {
                if (1 == 2)
                {
                    // we got major issues !!!!!
                }
            }
            int endid = bis.ReadInt32();
            if (endid != id)
            {
                if (1 == 2)
                {
                    // we got major issues !!!!!
                }
            }

            // outputWrite("</" + groupName + ">")

            return true;

        }

        private string translatePSIVToVersion(string psivString)
        {
            // we hard wire a few just because
            // Select Case psivString
            // Case "2009.05"
            // Return "2009r5"
            // 
            // Case "2019.011"
            // Return "2019r1.1"
            // 
            // Case "2018.04"
            // Return "2018r4"
            // 
            // Case "2020.021"
            // Return "2020r2.1"
            // 
            // Case "2021.021"
            // Return "2021r2.1"
            // 
            // End Select

            // in general they look like
            // 2019.011 YYYY.RRB
            //    YYYY year
            //    RR   release
            //    B    bug fix
            string[] parts = psivString.Split(".");

            if (parts.Length < 1)
            {
                return "2019r1.1";
            }

            string yearStr;
            string releaseStr;
            string bugStr;
            try
            {
                yearStr = parts[0];
            }
            catch (IndexOutOfRangeException )
            {
                yearStr = "2019";
            }
            try
            {
                releaseStr = parts[1].Substring(0, 2);
            }
            catch (IndexOutOfRangeException )
            {
                releaseStr = "1";
            }
            try
            {
                bugStr = parts[1].Substring(2, 1);
            }
            catch ( IndexOutOfRangeException  )
            {
                bugStr = "";
            }


            StringBuilder relParts = new StringBuilder();
            Int64 yearInt64;
            Int64.TryParse(yearStr, out yearInt64);
            relParts.Append(yearInt64.ToString("0000"));


            relParts.Append("r");
            Int64 releaseInt64;
            Int64.TryParse(releaseStr, out releaseInt64);
            relParts.Append(releaseInt64.ToString("00")) ;

            if (bugStr.Trim() != "")
            {
                relParts.Append(".");
                Int64 bugInt64;
                Int64.TryParse(bugStr, out bugInt64);
                relParts.Append(bugInt64.ToString("0"));

            }


            return relParts.ToString();

        }


        private bool processGroupItem(int tag, EndiannessAwareBinaryReader bis)
        {

            string tagChars;
            tagChars = fourCharAsString(tag);

            if (tag == fourCharCode("CBhv"))
            { // control behaviour
                return readGroup(bis, "ControlBehavior");
            }
            else if (tag == fourCharCode("CIns"))
            { // constant instance
                return readGroup(bis, "ConstantInstance");
            }
            else if (tag == fourCharCode("clrR"))
            { // single color repr in a color asset
                return readGroup(bis, "ColorRepresentation");
            }
            else if (tag == fourCharCode("Cnst"))
            { // constant
                return readGroup(bis, "Constant");
            }
            else if (tag == fourCharCode("CPal"))
            { // color palette
                return skipGroup(bis, "ColorPalette");
            }
            else if (tag == fourCharCode("CPrg"))
            { // computed property getter source
                return readGroup(bis, "GetAccessor");
            }
            else if (tag == fourCharCode("CPrs"))
            { // computed property setter source
                return readGroup(bis, "SetAccessor");
            }
            else if (tag == fourCharCode("Ctrl"))
            { // control
                return readGroup(bis, "Control");
            }
            else if (tag == fourCharCode("Ctrl"))
            { // control
                return readGroup(bis, "Control");
            }
            else if (tag == fourCharCode("Dmth"))
            {  // delegate
                return readGroup(bis, "DelegateDeclaration");
            }
            else if (tag == fourCharCode("elem"))
            { // icon element
                return readGroup(bis, "Element");
            }
            else if (tag == fourCharCode("Enum"))
            { // view prop enumerated values
                return readGroup(bis, "Enumeration");
            }
            else if (tag == fourCharCode("fTyp"))
            { // single type type entry
                return readGroup(bis, "FileType");
            }
            else if (tag == fourCharCode("HIns"))
            { // hook instance (event)
                return readGroup(bis, "HookInstance");
            }
            else if (tag == fourCharCode("HLCn"))
            { // constraints
                return readGroup(bis, "HighLevelConstraint");
            }
            else if (tag == fourCharCode("Hook"))
            { // event def
                return readGroup(bis, "Hook");
            }
            else if (tag == fourCharCode("Icon"))
            {  // control behaviour
                return readGroup(bis, "Icon");
            }
            else if (tag == fourCharCode("ImgR"))
            { // image representation
                return readGroup(bis, "ImageRepresentation");
            }
            else if (tag == fourCharCode("ImgS"))
            { // image spec
                return readGroup(bis, "ImageSpecification");
            }
            else if (tag == fourCharCode("iSCI"))
            { // ScreenContentItem
                return readGroup(bis, "ScreenContentItem");
            }
            else if (tag == fourCharCode("Meth"))
            { // method
                return readGroup(bis, "Method");
            }
            else if (tag == fourCharCode("MItm"))
            { // menuItem
                return readGroup(bis, "MenuItem");
            }
            else if (tag == fourCharCode("MnuH"))
            { // menu handler
                return readGroup(bis, "MenuHandler");
            }
            else if (tag == fourCharCode("Note"))
            { // note
                return readGroup(bis, "Note");
            }
            else if (tag == fourCharCode("PDef"))
            { // propval
                return processPropertyValue(bis);
            }
            else if (tag == fourCharCode("Prop"))
            { // property
                return readGroup(bis, "Property");
            }
            else if (tag == fourCharCode("Rpsc"))
            { // report section
                return readGroup(bis, "ReportSection");
            }
            else if (tag == fourCharCode("SEdr"))
            { // ui state editor
                return readGroup(bis, "Editor");
            }
            else if (tag == fourCharCode("SEds"))
            { // ui state editors
                return readGroup(bis, "Editors");
            }
            else if (tag == fourCharCode("segC"))
            { // segmented control
                return readGroup(bis, "SegmentedControl");
            }
            else if (tag == fourCharCode("sorc"))
            { // source lines
                return readGroup(bis, "ItemSource");
            }
            else if (tag == fourCharCode("Strx"))
            { // structure
                return readGroup(bis, "Structure");
            }
            else if (tag == fourCharCode("SwSt"))
            { // StudioWindowState
                return readGroup(bis, "StudioWindowState");
            }
            else if (tag == fourCharCode("ti  "))
            { // toolbar items
                return readGroup(bis, "ToolItem");
            }
            else if (tag == fourCharCode("USng"))
            { // using clause
                return readGroup(bis, "Using");
            }
            else if (tag == fourCharCode("VwBh"))
            { // view behaviour
                return readGroup(bis, "ViewBehavior");
            }
            else if (tag == fourCharCode("VwPr"))
            { // view prop
                return readGroup(bis, "ViewProperty");
            }
            else if (tag == fourCharCode("WrnP"))
            {
                return readGroup(bis, "WarningPreferences");
            }
            else if (tag == fourCharCode("WSSG"))
            { // web style state group
                return readGroup(bis, "WebStyleStateGroup");
            }
            else if (tag == fourCharCode("XMth"))
            { // external method
                return readGroup(bis, "ExternalMethod");
            }
            else if (tag == fourCharCode("FDef"))
            { // this makes the tags for this item as a wrapper NOT get emitted
                return readGroup(bis, "");
            }
            else if (tag == fourCharCode("Dseg"))
            { // 2021r3 desktop segmented ... yeah
                return readGroup(bis, "DesktopSegmentedButton");
            }
            else
            {
                return false;
            }

        }

        private bool readGroup(EndiannessAwareBinaryReader bis, string groupName)
        {
            // and its value type
            int grouptypetag;
            grouptypetag = bis.ReadInt32(); // ALWAYS Grup !
            string grouptypeTagChars;
            grouptypeTagChars = fourCharAsString(grouptypetag);
            if (fourCharCode("Grup") != grouptypetag)
            {
                bis.BaseStream.Position = bis.BaseStream.Position - 4;
                return false;
            }

            int size;
            size = bis.ReadInt32();
            int id;
            id = bis.ReadInt32();
            int dataSize;
            dataSize = size - 4;

            bool suppressGroup = false;

            if ("" == groupName)
            {
                suppressGroup = true;
            }

            if (false == suppressGroup)
            {
                outputWrite("<" + groupName + ">");
            }

            int propNameTag;
            propNameTag = bis.ReadInt32();
            string propNameTagChars;
            propNameTagChars = fourCharAsString(propNameTag);


            while (fourCharCode("EndG") != propNameTag)
            {
                // certain tags are handled specially
                if (false == processGroupItem(propNameTag, bis))
                {

                    // and its value type
                    int typetag;
                    typetag = bis.ReadInt32();
                    string typeTagChars;
                    typeTagChars = fourCharAsString(typetag);

                    string value;
                    value = ConvertTypeToString(bis, typetag);


                    if (ContainsLowBytes(value))
                    {
                        value = MakeHexBytesValue(value);
                    }
                    else if (fourCharCode("Rect") == typetag)
                    {
                        // do nothing !
                    }
                    else
                    {
                        value = MakeXMLSafe(value);
                    }


                    string xmlTag;
                    xmlTag = ConvertRBBFTagToXMLTag(propNameTag);


                    if (false == suppressGroup && "" != xmlTag)
                    {
                        outputWrite("<" + xmlTag + ">" + value + "</" + xmlTag + ">");
                    }

                }

                propNameTag = bis.ReadInt32();
                propNameTagChars = fourCharAsString(propNameTag);

            }

            int endtype;
            endtype = bis.ReadInt32();
            if (fourCharCode("Int ") != endtype)
            {
                if (1 == 2) { }
            }
            int endid;
            endid = bis.ReadInt32();
            if (endid != id)
            {
                if (1 == 2)
                {
                    // we got major issues !!!!!
                }
            }

            if (false == suppressGroup)
            {
                outputWrite("</" + groupName + ">");
            }


            return true;
        }

        private string ConvertRBBFTagToXMLTag(int rbbfTag)
        {
            string tagChars = fourCharAsString(rbbfTag);


            if ( Tags.ContainsKey(tagChars) )
            {
                return Tags.GetValueOrDefault(tagChars);
            } else {
                if (1 == 2)
                {
                    // we got major issues !!!!!
                }
                return "";
            }
            

           


        }

        private string fourCharAsString(int tagValue)
        {
            char[] letters = new char[4];

            letters[0] = (char)((tagValue & 0xFF000000) >> 24);
            letters[1] = (char)((tagValue & 0x00FF0000) >> 16);
            letters[2] = (char)((tagValue & 0x0000FF00) >> 8);
            letters[3] = (char)((tagValue & 0x000000FF) >> 0);

            return new string(letters);

        }

        private bool ContainsLowBytes(string s)
        {

            for (int ctr = 0; ctr < s.Length; ctr++)
            {
                if (System.Globalization.UnicodeCategory.Control == Char.GetUnicodeCategory(s[ctr]))
                {
                    return true;
                }

            }

            return false;
        }

        private string ConvertTypeToString(EndiannessAwareBinaryReader bis, int inTag)
        {
            if (inTag == fourCharCode("Strn"))
            {
                return readStringFromStream(bis);
            }
            else if (inTag == fourCharCode("Int "))
            {
                int tmpInt32;
                tmpInt32 = bis.ReadInt32();
                return tmpInt32.ToString("D"); // return as a decimal with sign
            }
            else if (inTag == fourCharCode("Dbl "))
            {
                double tmpDbl;
                tmpDbl = bis.ReadDouble();
                return tmpDbl.ToString("F2"); // floatng point with 6 decimals
            }
            else if (inTag == fourCharCode("Padn"))
            {
                eatPadding(bis);
                return "";
            }
            else if (inTag == fourCharCode("Rect"))
            {
                int tmpInt32_1;
                int tmpInt32_2;
                int tmpInt32_3;
                int tmpInt32_4;
                tmpInt32_1 = bis.ReadInt32();
                tmpInt32_2 = bis.ReadInt32();
                tmpInt32_3 = bis.ReadInt32();
                tmpInt32_4 = bis.ReadInt32();

                return "<Rect left=\"" + tmpInt32_1.ToString() + "\" top=\"" + tmpInt32_2.ToString() + "\" width=\"" + tmpInt32_3.ToString() + "\" height=\"" + tmpInt32_4.ToString() + "\"/>";
            }
            else
            {

                string unhandledTag;
                unhandledTag = fourCharAsString(inTag);
                //Print("unhandled data type tag " + unhandledTag)
                if (1 == 2)
                {
                    // we got major issues !!!!!
                }
                return "";
            }

        }

        private string convertRBBFBlockTagToXMLBlockTag(int int32Tag)
        {

            string tagChars;
            tagChars = fourCharAsString(int32Tag);

            try
            {
                return blockTags[tagChars];
            }
            catch (KeyNotFoundException)
            {
                Console.WriteLine($"Key = \"{tagChars}\" is not found.");
                return "";
            }

        }

        private void loadTags(int tagsetID)
        {

            switch (tagsetID)
            {
                case 1:
                    blockTags = format_1_blockTags();
                    break;
                case 2:
                    blockTags = format_2_blockTags();
                    break;
                default:
                    break;

            }

            Tags = new Dictionary<string, string>();

            Tags.Add("aivi", "AutoIncVersion");
            Tags.Add("Alas", "AliasName");
            Tags.Add("alis", "FileAlias");
            Tags.Add("Arch", "");
            Tags.Add("bApO", "IsApplicationObject");
            Tags.Add("BCar", "BuildCarbonMachOName");
            Tags.Add("bCls", "IsClass");
            Tags.Add("BCMO", "BuildCarbonMachOName");
            Tags.Add("bFAS", "BuildForAppStore");
            Tags.Add("Bflg", "BuildFlags");
            Tags.Add("bhlp", "ItemHelp");
            Tags.Add("binE", "BinaryEnum");
            Tags.Add("BL86", "BuildLinuxX86Name");
            Tags.Add("BMac", "");
            Tags.Add("BMDI", "BuildWinMDI");
            Tags.Add("BMSz", "");
            Tags.Add("bNtr", "IsInterface");
            Tags.Add("BSiz", "");
            Tags.Add("BunI", "BundleIdentifier");
            Tags.Add("BWin", "BuildWinName");
            Tags.Add("CBix", "ControlIndex");
            Tags.Add("ccls", "ControlClass");
            Tags.Add("Ci1a", "HLCItem1Attr");
            Tags.Add("Ci2a", "HLCItem2Attr");
            Tags.Add("CLan", "CurrentLanguage");
            Tags.Add("clr1", "ColorLight");
            Tags.Add("clr2", "ColorDark");
            Tags.Add("clrp", "ColorPlatform");
            Tags.Add("clrt", "ColorType");
            Tags.Add("cnfT", "ConformsTo");
            Tags.Add("Cni1", "HLCItem1");
            Tags.Add("Cni2", "HLCItem2");
            Tags.Add("CnLk", "HLCEditable");
            Tags.Add("CnMP", "HLCScale");
            Tags.Add("CnPr", "HLCPriority");
            Tags.Add("CnPv", "HLCValue");
            Tags.Add("CnRo", "HLCRelOp");
            Tags.Add("comM", "Comment");
            Tags.Add("Comp", "Compatibility");
            Tags.Add("Cont", "ObjContainerID");
            Tags.Add("cRDW", "CopyWindowsRedist");
            Tags.Add("data", "ItemData");
            Tags.Add("decl", "ItemDeclaration");
            Tags.Add("defn", "ItemDef");
            Tags.Add("DEnc", "DefaultEncoding");
            Tags.Add("Dest", "Subdirectory");
            Tags.Add("deVi", "Device");
            Tags.Add("devT", "DeviceType");
            Tags.Add("DgCL", "DebuggerCommandLine");
            Tags.Add("dhlp", "");
            Tags.Add("dkmd", "DarkMode");
            Tags.Add("DLan", "DefaultLanguage");
            Tags.Add("dscR", "Description");
            Tags.Add("DstR", "Destination");
            Tags.Add("DVew", "DefaultViewID");
            Tags.Add("Edpt", "EditingPartID");
            Tags.Add("enbl", "Enabled");
            Tags.Add("Enco", "TextEncoding");
            Tags.Add("EnVv", "EnvVars");
            Tags.Add("eSpt", "");
            Tags.Add("flag", "ItemFlags");
            Tags.Add("FTpt", "FilePhysicalType");
            Tags.Add("FTRk", "FileRank");
            Tags.Add("GDIp", "UseGDIPlus");
            Tags.Add("HCla", "HCLActive");
            Tags.Add("HCnm", "HLCName");
            Tags.Add("hidp", "HiDPI");
            Tags.Add("iArc", "IOSArchitecture");
            Tags.Add("Icon", "Icon");
            Tags.Add("iDDv", "IOSDebugDevice");
            Tags.Add("IDEv", "IDEVersion");
            Tags.Add("iLck", "Locked");
            Tags.Add("imPo", "Imported");
            Tags.Add("indx", "ItemIndex");
            Tags.Add("Intr", "Interfaces");
            Tags.Add("ioPP", "ProvisioningProfileName");
            Tags.Add("iOri", "IOSLayoutEditorViewOrientation");
            Tags.Add("iOsC", "IOSCapabilities");
            Tags.Add("isBn", "BuildiOSName");
            Tags.Add("itHd", "HeightDouble");
            Tags.Add("itHt", "Height");
            Tags.Add("itWd", "Width");
            Tags.Add("itwD", "WidthDouble");
            Tags.Add("IVer", "InfoVersion");
            Tags.Add("iVTy", "IOSLayoutEditorViewType");
            Tags.Add("kUTI", "UTIType");
            Tags.Add("lang", "ItemLanguage");
            Tags.Add("Lib ", "LibraryName");
            Tags.Add("linA", "LinuxArchitecture");
            Tags.Add("lncs", "");
            Tags.Add("lstH", "");
            Tags.Add("lstV", "");
            Tags.Add("LVer", "LongVersion");
            Tags.Add("macA", "MacArchitecture");
            Tags.Add("MacC", "MacCreator");
            Tags.Add("maEn", "MenuAutoEnable");
            Tags.Add("MaxW", "WindowMaximized");
            Tags.Add("MDIc", "WinMDICaption");
            Tags.Add("MiMk", "MenuShortcutModifier");
            Tags.Add("mimT", "MimeType");
            Tags.Add("MiSK", "MenuShortcut");
            Tags.Add("mVis", "MenuItemVisible");
            Tags.Add("name", "ItemName");
            Tags.Add("Name", "ObjName");
            Tags.Add("ndsc", "");
            Tags.Add("ndsr", "");
            Tags.Add("NnRl", "NonRelease");
            Tags.Add("ntln", "NoteLine");
            Tags.Add("objC", "ObjectiveC");
            Tags.Add("ocls", "WebObjectClass");
            Tags.Add("OPSp", "");
            Tags.Add("oPtL", "OptimizationLevel");
            Tags.Add("orie", "Orientation");
            Tags.Add("Padn", "");
            Tags.Add("parm", "ItemParams");
            Tags.Add("pasw", "");
            Tags.Add("path", "FullPath");
            Tags.Add("PDef", "PropertyVal");
            Tags.Add("plFM", "Platform");
            Tags.Add("pltf", "ItemPlatform");
            Tags.Add("ppth", "PartialPath");
            Tags.Add("PrGp", "PropertyGroup");
            Tags.Add("prTp", "ProjectType");
            Tags.Add("prWA", "WebApp");
            Tags.Add("PSIV", "ProjectSavedInVers");
            Tags.Add("PtID", "PartID");
            Tags.Add("PVal", "PropertyValue");
            Tags.Add("rEdt", "EditBounds");
            Tags.Add("Regn", "Region");
            Tags.Add("Rels", "Release");
            Tags.Add("resZ", "Resolution");
            Tags.Add("rslt", "ItemResult");
            Tags.Add("runA", "WindowsRunAs");
            Tags.Add("SCtx", "ScriptText");
            Tags.Add("scut", "ItemShortcut");
            Tags.Add("SEdC", "EditorCount");
            Tags.Add("SEId", "EditorIndex");
            Tags.Add("SELn", "EditorLocation");
            Tags.Add("SEPt", "EditorPath");
            Tags.Add("shrd", "IsShared");
            Tags.Add("size", "");
            Tags.Add("Size", "");
            Tags.Add("Soft", "SoftLink");
            Tags.Add("spmu", "ItemSpecialMenu");
            Tags.Add("srcl", "SourceLine");
            Tags.Add("StpA", "StepAppliesTo");
            Tags.Add("stsc", "");
            Tags.Add("stsr", "");
            Tags.Add("StST", "SelectedTab");
            Tags.Add("styl", "ItemStyle");
            Tags.Add("Supr", "Superclass");
            Tags.Add("SVer", "ShortVersion");
            Tags.Add("svin", "SaveInfo");
            Tags.Add("SySF", "SystemFlags");
            Tags.Add("Targ", "Target");
            Tags.Add("text", "ItemText");
            Tags.Add("TVew", "DefaultTabletViewID");
            Tags.Add("type", "ItemType");
            Tags.Add("UsBF", "UseBuildsFolder");
            Tags.Add("Usin", "GlobalUsingClauses");
            Tags.Add("vbET", "EditorType");
            Tags.Add("Ver1", "MajorVersion");
            Tags.Add("Ver2", "MinorVersion");
            Tags.Add("Ver3", "SubVersion");
            Tags.Add("Vsbl", "Visible");
            Tags.Add("VwBh", "ViewBehavior");
            Tags.Add("WbAn", "WebHostingAppName");
            Tags.Add("WbDS", "WebDisconnectString");
            Tags.Add("WbHd", "WebHostingDomain");
            Tags.Add("WbHI", "WebHostingIdentifier");
            Tags.Add("WbLS", "WebLaunchString");
            Tags.Add("WcmN", "BuildWinCompanyName");
            Tags.Add("Wdpt", "WebDebugPort");
            Tags.Add("Web2", "WebVersion");
            Tags.Add("WHTM", "WebHTMLHeader");
            Tags.Add("WiFd", "BuildWinFileDescription");
            Tags.Add("winA", "WindowsArchitecture");
            Tags.Add("WiNm", "BuildWinInternalName");
            Tags.Add("wInV", "WebControlInitialValue");
            Tags.Add("WinV", "WindowsVersions");
            Tags.Add("Wpcl", "WebProtocol");
            Tags.Add("WpNm", "BuildWinProductName");
            Tags.Add("Wprt", "WebPort");
            Tags.Add("WptS", "WebSecurePort");
            Tags.Add("WSSI", "WebStyleStateID");

        }

        private Dictionary<string, string> format_1_blockTags()
        {
            blockTags = new Dictionary<string, string>();

            blockTags.Add("Aicn", "ApplicationIcon");
            blockTags.Add("BSbu", "BuildProjectStep");
            blockTags.Add("BScf", "CopyFilesStep");
            blockTags.Add("Bsls", "BuildStepsList");
            blockTags.Add("BSsc", "IDEScriptStep");
            blockTags.Add("BSsn", "SignProjectScriptStep");
            blockTags.Add("BSts", "BuildAutomation");
            blockTags.Add("colr", "ColorAsset");
            blockTags.Add("IEsx", "ExternalScriptStep");
            blockTags.Add("Img ", "MultiImage");
            blockTags.Add("ioLS", "IOSLaunchScreen");
            blockTags.Add("iosv", "IOSView");
            blockTags.Add("Limg", "LaunchImages");
            blockTags.Add("mobv", "MobileScreen");
            blockTags.Add("pExt", "ExternalCode");
            blockTags.Add("pFol", "Folder");
            blockTags.Add("pFTy", "FileTypes");
            blockTags.Add("pLay", "IOSLayout");
            blockTags.Add("pMnu", "Menu");
            blockTags.Add("pObj", "Module");
            blockTags.Add("Proj", "Project");
            blockTags.Add("pRpt", "Report");
            blockTags.Add("pScn", "IOSScreen");
            blockTags.Add("pTbr", "Toolbar");
            blockTags.Add("pUIs", "UIState");
            blockTags.Add("pVew", "Window");
            blockTags.Add("pWPg", "WebPage");
            blockTags.Add("pWSe", "WebSession");
            blockTags.Add("pWSt", "WebStyle");
            blockTags.Add("WrKr", "Worker");
            blockTags.Add("xWbC", "WebContainer");
            blockTags.Add("xWbV", "WebView");
            blockTags.Add("xWSs", "WebSession");

            return blockTags;
        }

        private Dictionary<string, string> format_2_blockTags()
        {
            blockTags = new Dictionary<string, string>();

            blockTags.Add("Aicn", "ApplicationIcon");
            blockTags.Add("BSbu", "BuildProjectStep");
            blockTags.Add("BScf", "CopyFilesStep");
            blockTags.Add("Bsls", "BuildStepsList");
            blockTags.Add("BSsc", "IDEScriptStep");
            blockTags.Add("BSsn", "SignProjectScriptStep");
            blockTags.Add("BSts", "BuildAutomation");
            blockTags.Add("colr", "ColorAsset");
            blockTags.Add("IEsx", "ExternalScriptStep");
            blockTags.Add("Img ", "MultiImage");
            blockTags.Add("ioLS", "IOSLaunchScreen");
            blockTags.Add("iosv", "IOSView");
            blockTags.Add("Limg", "LaunchImages");
            blockTags.Add("mobv", "MobileScreen");
            blockTags.Add("pExt", "ExternalCode");
            blockTags.Add("pFol", "Folder");
            blockTags.Add("pFTy", "FileTypes");
            blockTags.Add("pLay", "IOSLayout");
            blockTags.Add("pMnu", "Menu");
            blockTags.Add("pObj", "Module");
            blockTags.Add("Proj", "Project");
            blockTags.Add("pRpt", "Report");
            blockTags.Add("pScn", "IOSScreen");
            blockTags.Add("pTbr", "Toolbar");
            blockTags.Add("pUIs", "UIState");
            blockTags.Add("pVew", "Window");
            blockTags.Add("pWPg", "WebPage");
            blockTags.Add("pWSe", "WebSession");
            blockTags.Add("pWSt", "WebStyle");
            blockTags.Add("WrKr", "Worker");
            blockTags.Add("xWbC", "WebContainer");
            blockTags.Add("xWbV", "WebView");
            blockTags.Add("xWSs", "WebSession");
            blockTags.Add("pDWn", "DesktopWindow");

            return blockTags;

        }

        private string MakeHexBytesValue(string value)
        {
            Encoding ascii = Encoding.ASCII;
            byte[] strBytes = ascii.GetBytes(value);
            return MakeHexBytesValue(strBytes);
        }

        private string MakeHexBytesValue(byte[] strBytes)
        {
            StringBuilder sb = new StringBuilder();

            foreach (byte b in strBytes)
            {
                string hexstring = "00" + b.ToString("X");
                sb.Append(hexstring.Substring(hexstring.Length - 2));
            }

            return "<Hex bytes=\"" + strBytes.Length.ToString() + "\">" + sb.ToString() + "</Hex>";
        }

        private string MakeXMLSafe(string inStr)
        {
            string tmp = inStr;

            if (tmp.Length >= 6 && tmp.Substring(0, 6) == "&amp;h")
            {
                return tmp;
            }
            if (tmp.Length >= 6 && tmp.Substring(0, 6) == "&amp;H")
            {
                return tmp;
            }
            if (tmp.Length >= 6 && tmp.Substring(0, 6) == "&amp;c")
            {
                return tmp;
            }
            if (tmp.Length >= 6 && tmp.Substring(0, 6) == "&amp;C")
            {
                return tmp;
            }
            tmp = tmp.Replace("&", "&amp;");
            tmp = tmp.Replace("<", "&lt;");
            tmp = tmp.Replace(">", "&gt;");
            tmp = tmp.Replace("'", "&apos;");

            return tmp;

        }

        private string readStringFromStream(EndiannessAwareBinaryReader bis)
        {
            long readStart = bis.BaseStream.Position;

            int bytesToRead = bis.ReadInt32();

            int actualBytesToRead = bytesToRead;

            // we round this up to the nearest multiple of 4
            if ((actualBytesToRead % 4) != 0)
            {
                actualBytesToRead = ((actualBytesToRead / 4) + 1) * 4;
            }

            byte[] bytedata = bis.ReadBytes(actualBytesToRead);
                      
            string data;
            try
            {
                data = System.Text.Encoding.ASCII.GetString(bytedata, 0, bytedata.Length);
                // int dataLen = data.Length;
                data = data.Substring(0, bytesToRead);
                // dataLen = data.Length;
            }
            catch
            {
                if (1 == 2)
                {
                    // we got major issues !!!!!
                }
                data = ""; 
            }
            return data;

        }

        private void eatPadding(EndiannessAwareBinaryReader bis)
        {
            // now another tag
            // size             4 bytes
            // size * bytes     

            int size = bis.ReadInt32();

            bis.BaseStream.Position = bis.BaseStream.Position + size;

        }

        private bool processPropertyValue(EndiannessAwareBinaryReader bis)
        {
            // and its value type
            int grouptypetag = bis.ReadInt32(); // ALWAYS Grup !
            string grouptypeTagChars;
            grouptypeTagChars = fourCharAsString(grouptypetag);
            if(grouptypetag != fourCharCode("Grup"))
            {
                bis.BaseStream.Position = bis.BaseStream.Position - 4;
                return false;
            }

            int size = bis.ReadInt32();
            int id = bis.ReadInt32();
            int dataSize;
            dataSize = size - 4;


            string name = "";
            string type;
            string group;
            bool visible;
            int Encoding;
            string propValue = "";


            int propNameTag = bis.ReadInt32();
            string propNameTagChars = fourCharAsString(propNameTag);


            while (propNameTag != fourCharCode("EndG"))
            {
                // and its value type
                int typetag = bis.ReadInt32();
                string typeTagChars;
                typeTagChars = fourCharAsString(typetag);

                string value = ConvertTypeToString(bis, typetag);
                string xmlsafevalue = MakeXMLSafe(value);

                if (propNameTag == fourCharCode("name"))
                {
                    name = xmlsafevalue;
                }
                else if (propNameTag == fourCharCode("type"))
                {
                    type = xmlsafevalue;
                }
                else if (propNameTag == fourCharCode("PrGp"))
                {
                    group = xmlsafevalue;
                }
                else if (propNameTag == fourCharCode("visi"))
                {
                    visible = Convert.ToInt32(xmlsafevalue) == 0;
                }
                else if (propNameTag == fourCharCode("Enco"))
                {
                    Encoding = kUTF8;
                }
                else if (propNameTag == fourCharCode("PVal"))
                {
                    propValue = xmlsafevalue;
                }

                propNameTag = bis.ReadInt32();
                propNameTagChars = fourCharAsString(propNameTag);
            }

            int endtype = bis.ReadInt32();
            if (endtype != fourCharCode("Int "))
            {
                if (1 == 2) { }
            }

            int endid = bis.ReadInt32();
            if (endid != id)
            {
                if (1 == 2)
                {
                    // we got major issues !!!!!
                }
            }

            if (ContainsLowBytes(propValue))
            {
                propValue = MakeHexBytesValue(propValue);
            }
            else
            {
                propValue = MakeXMLSafe(propValue);
            }
                       
            outputWrite("<PropertyVal Name=\"" + name + "\">" + propValue + "</PropertyVal>");

            return true;
        }

        private void outputWrite(string line)
        {
            Console.WriteLine(line);

            // as long as we havent set the "version" property we buffer lines
            if (mVersion != "")
                outputStream.WriteLine(line);
            else
                mBufferedLines.Add(line);

        }
    }
}