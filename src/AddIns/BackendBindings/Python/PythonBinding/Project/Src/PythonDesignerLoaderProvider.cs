﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.ComponentModel.Design.Serialization;
using ICSharpCode.FormsDesigner;
using ICSharpCode.Scripting;

namespace ICSharpCode.PythonBinding
{
	public class PythonDesignerLoaderProvider : IDesignerLoaderProviderWithViewContent
	{
		public PythonDesignerLoaderProvider()
		{
		}
		
		public IDesignerLoader CreateLoader(IDesignerGenerator generator)
		{
			return new PythonDesignerLoader(generator as IScriptingDesignerGenerator);
		}	
		
		public FormsDesignerViewContent ViewContent {
			get {
				throw new NotImplementedException();
			}
			set {
				throw new NotImplementedException();
			}
		}
	}
}
