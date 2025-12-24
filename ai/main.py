import sqlite3
from typing import TypedDict, Annotated, List
from fastapi.middleware.cors import CORSMiddleware
from fastapi import FastAPI, Depends, HTTPException, status
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from pydantic import BaseModel

import httpx
from jose import jwt
from langchain_community.utilities import StackExchangeAPIWrapper
from langchain_community.tools import StackExchangeTool
from langchain_openai import ChatOpenAI
from langchain_core.messages import BaseMessage, HumanMessage, AIMessage
from langgraph.graph import StateGraph, START, END
from langgraph.graph.message import add_messages
from langgraph.checkpoint.sqlite import SqliteSaver
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
    model="openai/gpt-oss-120b",
    streaming=False,
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
def chat_node(state: Data):
    response = llmt.invoke(state["messages"])
    return {"messages": [response]}

tool_node = ToolNode(tools)

# =====================================================
# GRAPH SETUP
# =====================================================
conn = sqlite3.connect("bhb_test.db", check_same_thread=False)
checkpointer = SqliteSaver(conn)

graph = StateGraph(Data)
graph.add_node("chat_node", chat_node)
graph.add_node("tools", tool_node)

graph.add_edge(START, "chat_node")
graph.add_conditional_edges("chat_node", tools_condition)
graph.add_edge("tools", "chat_node")
graph.add_edge("chat_node", END)

app_graph = graph.compile(checkpointer=checkpointer)

# =====================================================
# FASTAPI
# =====================================================
api = FastAPI(title="BlackhatBadshah LangGraph API")

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
)

@api.post("/chat", response_model=ChatResponse)
def chat(
    req: ChatRequest,
    token=Depends(verify_jwt),  # 🔐 JWT protected
):
    result = app_graph.invoke(
        {"messages": [HumanMessage(content=req.message)]},
        config={"configurable": {"thread_id": req.thread_id}},
    )

    last = result["messages"][-1]

    if isinstance(last, AIMessage):
        return ChatResponse(reply=last.content)

    return ChatResponse(reply=str(last))


# =====================================================
# HEALTH
# =====================================================
@api.get("/health")
def health():
    return {"status": "ok"}
