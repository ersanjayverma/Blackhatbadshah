#!/usr/bin/env python3
"""
Simple test script to verify Claude API integration
Run with: python3 test_claude.py (requires langchain-anthropic installed)
"""

import asyncio
import os

async def test_claude():
    try:
        from langchain_anthropic import ChatAnthropic
        from langchain_core.messages import HumanMessage

        # Check API key
        api_key = os.environ.get("ANTHROPIC_API_KEY")
        if not api_key:
            print("❌ Error: ANTHROPIC_API_KEY environment variable not set")
            print("Please run: export ANTHROPIC_API_KEY='your-api-key'")
            return False

        print("✓ API key found")
        print("✓ Creating ChatAnthropic instance...")

        # Initialize Claude
        llm = ChatAnthropic(
            model="claude-sonnet-4-5-20250929",
            streaming=False,
        )

        print("✓ Sending test message to Claude...")

        # Test basic message
        response = await llm.ainvoke([HumanMessage(content="Say 'Hello from Claude!' in one sentence.")])

        print(f"\n✅ Success! Claude responded:")
        print(f"   {response.content}\n")

        # Test with tools (like in main.py)
        print("✓ Testing with tool binding...")
        from langchain_community.tools import DuckDuckGoSearchRun

        tool_search = DuckDuckGoSearchRun(region="in-en")
        llmt = llm.bind_tools([tool_search])

        response = await llmt.ainvoke([HumanMessage(content="What is 2+2?")])
        print(f"✅ Tool binding works! Response type: {type(response)}\n")

        return True

    except ImportError as e:
        print(f"❌ Import Error: {e}")
        print("\nPlease install required packages:")
        print("  pip install langchain-anthropic langchain-community ddgs")
        return False
    except Exception as e:
        print(f"❌ Error: {e}")
        return False

if __name__ == "__main__":
    print("=" * 60)
    print("Testing Claude API Integration")
    print("=" * 60)
    success = asyncio.run(test_claude())
    print("=" * 60)
    exit(0 if success else 1)
