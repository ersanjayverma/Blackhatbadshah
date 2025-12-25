import aiosqlite
from langgraph.checkpoint.sqlite.aio import AsyncSqliteSaver
from contextlib import asynccontextmanager
from typing import TypedDict, Annotated, List
from fastapi.middleware.cors import CORSMiddleware
from fastapi import FastAPI, Depends, HTTPException, status,Request
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from pydantic import BaseModel
from fastapi.responses import StreamingResponse
import httpx
import json
import asyncio
from jose import jwt
from langchain_community.utilities import StackExchangeAPIWrapper
from langchain_community.tools import StackExchangeTool
from langchain_openai import ChatOpenAI
from langchain_core.messages import BaseMessage, HumanMessage, AIMessage,AIMessageChunk
from langgraph.graph import StateGraph, START, END
from langgraph.graph.message import add_messages
from langgraph.prebuilt import ToolNode, tools_condition
from langchain_community.tools import DuckDuckGoSearchRun, WikipediaQueryRun
from langchain_community.utilities import WikipediaAPIWrapper

# =====================================================
# JWT / KEYCLOAK CONFIG  (matches ASP.NET config)
# =====================================================
AUTHORITY = "https://auth.blackhatbadshah.com/realms/blackhatbadshah"
AUDIENCE = "blackhatbadshah-api"
ALGORITHMS = ["RS256"]
JWKS_URL = f"{AUTHORITY}/protocol/openid-connect/certs"

security = HTTPBearer()
_jwks_cache = None

@asynccontextmanager
async def lifespan(api: FastAPI):
    async with aiosqlite.connect("bhb_test.db") as conn:
        saver = AsyncSqliteSaver(conn)
        api.state.graph = graph.compile(checkpointer=saver)
        yield

# =====================================================
# FASTAPI
# =====================================================
api = FastAPI(
    title="BlackhatBadshah LangGraph API",
    lifespan=lifespan,
)

async def verify_jwt(
    credentials: HTTPAuthorizationCredentials = Depends(security),
):
    global _jwks_cache
    token = credentials.credentials

    try:
        if not _jwks_cache:
            async with httpx.AsyncClient() as client:
                resp = await client.get(JWKS_URL)
                resp.raise_for_status()
                _jwks_cache = resp.json()

        payload = jwt.decode(
            token,
            _jwks_cache,
            algorithms=ALGORITHMS,
            audience=AUDIENCE,
            issuer=AUTHORITY,
        )

        return payload

    except Exception:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid or expired token",
        )


# =====================================================
# LLM
# =====================================================
llm = ChatOpenAI(
    openai_api_base="https://api.together.xyz/v1",
    openai_api_key="2d15d7147c32f76cd01c30754ba484012d106ac462a5b1d269a2a5afb9036e8f",
    model="Qwen/Qwen3-Coder-480B-A35B-Instruct-FP8",
    streaming=True,
)

# =====================================================
# LANGGRAPH STATE
# =====================================================
class Data(TypedDict):
    messages: Annotated[List[BaseMessage], add_messages]

# =====================================================
# TOOLS
# =====================================================
tool_search = DuckDuckGoSearchRun(region="in-en")
stack_api = StackExchangeAPIWrapper()
tool_stack = StackExchangeTool(api_wrapper=stack_api)
wiki_api = WikipediaAPIWrapper(
    top_k_results=3,
    doc_content_chars_max=5000
)
tool_wiki = WikipediaQueryRun(api_wrapper=wiki_api)

tools = [tool_search, tool_wiki,tool_stack]
llmt = llm.bind_tools(tools)

# =====================================================
# GRAPH NODES
# =====================================================
async def chat_node(state: Data):
    response = await llmt.ainvoke(state["messages"])
    return {"messages": [response]}

tool_node = ToolNode(tools)

# =====================================================
# GRAPH SETUP
# =====================================================
graph = StateGraph(Data)
graph.add_node("chat_node", chat_node)
graph.add_node("tools", tool_node)

graph.add_edge(START, "chat_node")
graph.add_conditional_edges("chat_node", tools_condition)
graph.add_edge("tools", "chat_node")
graph.add_edge("chat_node", END)




class ChatRequest(BaseModel):
    thread_id: str
    message: str

class ChatResponse(BaseModel):
    reply: str

api.add_middleware(
    CORSMiddleware,
    allow_origins=[
        "https://blackhatbadshah.com",
        "https://www.blackhatbadshah.com",
        "https://api.blackhatbadshah.com",
        "https://www.api.blackhatbadshah.com"
    ],
    allow_credentials=True,
    allow_methods=["GET", "POST", "OPTIONS"],
    allow_headers=[
        "Authorization",
        "Content-Type",
    ],
    expose_headers=["Content-Type", "X-Accel-Buffering"]
)

@api.post("/chat", response_model=ChatResponse)
async def chat(
    req: ChatRequest,
    request: Request,
    token=Depends(verify_jwt),
):
    try:
        result = await request.app.state.graph.ainvoke(
            {"messages": [HumanMessage(content=req.message)]},
            config={"configurable": {"thread_id": req.thread_id}},
        )
    except Exception as e:
        # Never crash the API
        return ChatResponse(reply=f"Analyzer failed: {str(e)}")

    messages = result.get("messages", [])
    if not messages:
        return ChatResponse(reply="Analyzer returned no messages")

    last = messages[-1]

    if isinstance(last, AIMessage):
        return ChatResponse(reply=last.content)

    # Fallback (should rarely happen)
    return ChatResponse(reply=str(last))



@api.post("/chat/stream")
async def chat_stream(
    req: ChatRequest,
    request: Request, 
    token=Depends(verify_jwt),
):
    async def event_generator():
        try:
            yield ": connected\n\n"

            # Use astream with "messages" mode
            async for chunk in request.app.state.graph.astream(
                {"messages": [HumanMessage(content=req.message)]},
                config={"configurable": {"thread_id": req.thread_id}},
                stream_mode="messages",
            ):
                # 1. Handle Tuple Unpacking: chunk is often (AIMessageChunk, metadata)
                msg = chunk[0] if isinstance(chunk, tuple) else chunk
                
                # 2. Extract content from AIMessageChunk or dict
                content = ""
                if isinstance(msg, AIMessageChunk):
                    content = msg.content
                elif isinstance(msg, dict) and "content" in msg:
                    content = msg["content"]
                elif hasattr(msg, "content"):
                    content = msg.content

                # 3. Yield only if there is actual text
                if content:
                    yield f"data: {json.dumps({'token': content})}\n\n"

            yield "data: [DONE]\n\n"

        except Exception as e:
            # Detailed logging to help you see the exact failure in the console
            import traceback
            error_detail = traceback.format_exc()
            print(f"CRITICAL STREAM ERROR:\n{error_detail}")
            
            # Send a cleaner error to the client
            yield f"data: {json.dumps({'error': str(e)})}\n\n"

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream",
        headers={
            "Content-Type": "text/event-stream",
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "X-Accel-Buffering": "no",  # CRITICAL: Disables Nginx buffering
        },
    )


# =====================================================
# HEALTH
# =====================================================
@api.get("/health")
async def health():
    return {"status": "ok"}
