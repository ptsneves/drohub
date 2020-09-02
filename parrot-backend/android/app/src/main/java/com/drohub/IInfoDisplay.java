package com.drohub;

public interface IInfoDisplay {
    void addError(String msg);
    void removeError(String msg);
    void addErrorTemporarily(String msg, long time_available);
}