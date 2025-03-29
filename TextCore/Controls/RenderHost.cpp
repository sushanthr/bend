// Copyright (c) Microsoft Corporation.  All rights reserved.

#include "StdAfx.h"
#include "RenderHost.h"
#include "Windowsx.h"
#include "WinUser.h"

using namespace System;
using namespace System::Windows;
using namespace System::Windows::Interop;
using namespace System::Threading;
using namespace System::Runtime::InteropServices;
using namespace Microsoft::WindowsAPICodePack::DirectX::Controls;

bool RenderHost::RegisterWindowClass()
{
	WNDCLASS wndClass;

	if(GetClassInfo(m_hInstance, m_sClassName, &wndClass))
	{
		return true;
	}

	wndClass.style				= CS_HREDRAW | CS_VREDRAW  | CS_DBLCLKS;
	
	wndClass.lpfnWndProc		= DefWindowProc; 
	wndClass.cbClsExtra			= 0;
	wndClass.cbWndExtra			= 0;
	wndClass.hInstance			= m_hInstance;
	wndClass.hIcon				= LoadIcon(NULL, IDI_WINLOGO);
	wndClass.hCursor			= LoadCursor(0, IDC_ARROW);
	wndClass.hbrBackground		= 0;
	wndClass.lpszMenuName		= NULL; // No menu
	wndClass.lpszClassName		= m_sClassName;

	if (!RegisterClass(&wndClass))
	{
		return false;
	}

	return true;
}

HandleRef RenderHost::BuildWindowCore(HandleRef hwndParent) 
{
	m_hInstance		= (HINSTANCE) GetModuleHandle(NULL);
	m_sWindowName	= L"RenderHost";
	m_sClassName	= L"RenderHost";

	if(RegisterWindowClass())
	{
		HWND parentHwnd = (HWND)hwndParent.Handle.ToPointer();

		m_hWnd = CreateWindowEx(0,
								m_sClassName,
								m_sWindowName,
								WS_CHILD | WS_VISIBLE,
								0,
								0,
								10, // These are arbitary values,
								10, // real sizes will be defined by the parent
								parentHwnd,
								NULL,
								m_hInstance,
								NULL );

		if(!m_hWnd)
		{
			return HandleRef(nullptr, System::IntPtr::Zero);
		}

		SetClassLong(m_hWnd,    // window handle 
			-12,      // change cursor 
			(LONG)LoadCursor(0, IDC_IBEAM));   // new cursor

        return HandleRef(this, IntPtr(m_hWnd));
    }

	return HandleRef(nullptr, System::IntPtr::Zero);
}

void RenderHost::DestroyWindowCore(HandleRef hwnd)
{
	if(NULL != m_hWnd && m_hWnd == (HWND)hwnd.Handle.ToPointer())
	{
		::DestroyWindow(m_hWnd);
		m_hWnd = NULL;
	}

	UnregisterClass(m_sClassName, m_hInstance);
}

void RenderHost::OnRender(DrawingContext ^ ctx)
{
	if (Render!= nullptr)
		Render();
}

IntPtr RenderHost::WndProc( IntPtr hwnd,  int msg,  IntPtr wParam,  IntPtr lParam, bool% handled)
{
	handled = false;
	switch (msg)
	{
	case WM_PAINT:
    case WM_DISPLAYCHANGE:
		InvalidateVisual();
		handled = false;
		break;
	case WM_SIZE:
  		InvalidateVisual();
		handled = false;
		break;
	case WM_LBUTTONDOWN :
	case WM_LBUTTONUP   :
	case WM_RBUTTONDOWN :
	case WM_RBUTTONUP   :
	case WM_MOUSEMOVE   :
	case WM_MOUSEWHEEL  :
	case WM_LBUTTONDBLCLK:
	    if (MouseHandler != nullptr)
		{
			int xPos = GET_X_LPARAM((int)lParam);
			int yPos = GET_Y_LPARAM((int)lParam);
			MouseHandler(xPos, yPos, msg, (Int64)wParam);
			handled = true;
		}
		break;
	case WM_CHAR:
	    if (KeyHandler != nullptr)
		{
			KeyHandler((int) wParam, (int) lParam);
			handled = true;
		}
		break;
	case WM_SETFOCUS:
	case WM_KILLFOCUS:
	    if (OtherHandler != nullptr)
		{
			OtherHandler(msg, (int) wParam, (int) lParam);
			handled = true;
		}
		break;
	}
    return IntPtr::Zero;
}
