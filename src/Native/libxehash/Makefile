CC = gcc
CXX = g++
CFLAGS = -g -Wall -c -fPIC -O2 -Wno-pointer-sign -Wno-char-subscripts -Wno-unused-variable -Wno-unused-function -Wno-strict-aliasing -Wno-discarded-qualifiers -Wno-unused-const-variable
CXXFLAGS = -g -Wall -fPIC -fpermissive -O2 -Wno-char-subscripts -Wno-unused-variable -Wno-unused-function -Wno-strict-aliasing -Wno-sign-compare -std=c++11
LDFLAGS = -shared
LDLIBS = -lsodium
TARGET  = libxehash.so

# **サブフォルダを含めた .cpp, .c ファイルを検索**
SOURCES = $(shell find . -type f \( -name "*.cpp" -o -name "*.c" \))
OBJECTS = $(SOURCES:.cpp=.o)
OBJECTS := $(OBJECTS:.c=.o)

all: $(TARGET)

$(TARGET): $(OBJECTS)
	$(CXX) $(LDFLAGS) -o $@ $^ $(LDLIBS)

# **C++ の .o ファイルを作成**
%.o: %.cpp
	@mkdir -p $(dir $@)  # サブフォルダの .o を格納するためのディレクトリ作成
	$(CXX) $(CXXFLAGS) -c $< -o $@

# **C の .o ファイルを作成**
%.o: %.c
	@mkdir -p $(dir $@)
	$(CC) $(CFLAGS) -c $< -o $@

clean:
	$(RM) $(TARGET) $(OBJECTS)
