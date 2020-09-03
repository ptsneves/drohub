package com.drohub;

public interface IInfoDisplay {
    void add(String msg);
    void remove(String msg);
    void addTemporarily(String msg, long time_available);
}